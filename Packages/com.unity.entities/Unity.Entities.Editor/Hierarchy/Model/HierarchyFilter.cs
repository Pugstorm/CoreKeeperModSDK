using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Represents a search query object which can be 'applied' to a set of nodes. This uses the shared state from <see cref="Hierarchy.HierarchySearch"/>.
    /// </summary>
    class HierarchyFilter : IDisposable
    {
        static readonly ProfilerMarker k_PreProcessTokensMarker = new ProfilerMarker($"{nameof(HierarchyFilter)}.{nameof(PreProcessTokens)}");

        /// <summary>
        /// The search backend for the hierarchy.
        /// </summary>
        readonly HierarchySearch m_HierarchySearch;

        /// <summary>
        /// The processed result of a search query. This is used to drive the underlying change tracking/differs.
        /// </summary>
        readonly HierarchyQueryBuilder.Result m_QueryResult;

        /// <summary>
        /// The pre-processed tokens from the search query. This is used to drive the bursted searching algorithms.
        /// </summary>
        NativeList<FixedString64Bytes> m_Tokens;

        readonly int m_EntityIndex;

        readonly NodeKind m_Kind;

        /// <summary>
        /// Gets the current active <see cref="FilterQueryDesc"/>.
        /// </summary>
        public EntityQueryDesc FilterQueryDesc => m_QueryResult.EntityQueryDesc;
        
        /// <summary>
        /// Returns the pre-processed tokens.
        /// </summary>
        public NativeArray<FixedString64Bytes> Tokens => m_Tokens.AsArray();

        /// <summary>
        /// Gets a value indicating if the filter is valid or not.
        /// </summary>
        public bool IsValid => m_QueryResult.IsValid;

        /// <summary>
        /// Returns why the filter is invalid.
        /// </summary>
        public string ErrorMsg { get; set; }

        public string ErrorCategory { get; set; }

        public delegate void ModifyEntityQuery(ref EntityQuery query);
        private static ModifyEntityQuery[] s_EmptyModifiers = new ModifyEntityQuery[0];
        public ModifyEntityQuery[] EntityQueryModifiers { get; set; }

        static readonly string k_ComponentTypeNotFoundTitle = L10n.Tr("Type not found");
        static readonly string k_ComponentTypeNotFoundContent = L10n.Tr("\"{0}\" is not a component type");

        internal HierarchyFilter(HierarchySearch hierarchySearch, string searchString, ICollection<string> tokens, Allocator allocator)
            : this(hierarchySearch, HierarchyQueryBuilder.BuildQuery(searchString), tokens, -1, NodeKind.None, allocator, true)
        {
        }

        internal HierarchyFilter(HierarchySearch hierarchySearch, HierarchyQueryBuilder.Result result, ICollection<string> tokens, int entityIndex, NodeKind nodeKind, Allocator allocator, bool parseTokensForFilter)
        {
            m_HierarchySearch = hierarchySearch;
            m_QueryResult = result;

            if (!string.IsNullOrEmpty(m_QueryResult.ErrorComponentType))
            {
                ErrorCategory = k_ComponentTypeNotFoundTitle;
                ErrorMsg = string.Format(k_ComponentTypeNotFoundContent, m_QueryResult.ErrorComponentType);
            }

            m_Tokens = new NativeList<FixedString64Bytes>(tokens?.Count ?? 1, allocator);
            m_EntityIndex = entityIndex;
            m_Kind = nodeKind;

            if (tokens != null)
            {
                if (parseTokensForFilter)
                {
                    PreProcessTokens(tokens, m_Tokens, ref m_EntityIndex, ref m_Kind);
                }
                else
                {
                    foreach (var t in tokens)
                        m_Tokens.Add(ProcessToken(t));
                    ProcessSearchValueTokens(m_Tokens);
                }
            }

            EntityQueryModifiers = s_EmptyModifiers;
        }

        public void Dispose()
        {
            m_Tokens.Dispose();
        }

        void PreProcessTokens(IEnumerable<string> inputTokens, NativeList<FixedString64Bytes> processedTokens, ref int targetIndex, ref NodeKind kind)
        {
            using var marker = k_PreProcessTokensMarker.Auto();

            // Ensure a clean list
            processedTokens.Clear();

            // Extract valid tokens
            foreach (var token in inputTokens)
            {
                // Component
                if (token.StartsWith(Constants.ComponentSearch.TokenOp, StringComparison.OrdinalIgnoreCase))
                    continue;
                else if (token.StartsWith(Constants.Hierarchy.EntityIndexTokenOpEqual, StringComparison.OrdinalIgnoreCase))
                {
                    var startIndex = Constants.Hierarchy.EntityIndexTokenOpEqual.Length;
                    var length = token.Length - startIndex;

                    if (length == 0)
                        continue;

                    var value = token.Substring(startIndex, token.Length - startIndex);

                    if (int.TryParse(value, out targetIndex))
                        continue;
                }
                else if (token.StartsWith(Constants.Hierarchy.NodeKindOpEqual, StringComparison.OrdinalIgnoreCase))
                {
                    var startIndex = Constants.Hierarchy.NodeKindOpEqual.Length;
                    var length = token.Length - startIndex;
                    if (length == 0)
                        continue;

                    var value = token.Substring(startIndex, token.Length - startIndex).ToLowerInvariant();
                    foreach(var k in Enum.GetValues(typeof(NodeKind)))
                    {
                        if (k.ToString().ToLowerInvariant() == value)
                            kind = (NodeKind)k;
                        continue;
                    }
                }
                else if (token.StartsWith("\"", StringComparison.Ordinal) && token.EndsWith("\"", StringComparison.Ordinal)) 
                {
                    // Discard single quote or empty double quotes
                    if (token.Length > 2)
                        processedTokens.Add(token.Substring(1, Math.Min(token.Length - 2, FixedString64Bytes.UTF8MaxLengthInBytes)).ToLowerInvariant());
                }
                else
                {
                    processedTokens.Add(ProcessToken(token));
                }
            }

            if (processedTokens.Length <= 1)
                return;

            ProcessSearchValueTokens(processedTokens);
        }

        static FixedString64Bytes ProcessToken(string token)
        {
            var truncatedToken = token.Length > FixedString64Bytes.UTF8MaxLengthInBytes ? token.Substring(0, FixedString64Bytes.UTF8MaxLengthInBytes) : token;

            // Use the fixed string to-lower variant to be compatible with filtering. 
            return FixedStringUtility.ToLower(truncatedToken);
        }

        void ProcessSearchValueTokens(NativeList<FixedString64Bytes> processedTokens)
        {
            // Factor out repeats by removing short strings contained in longer strings
            // e.g.: [GameObject, Object, a, z] -> [GameObject, z]
            for (var i = processedTokens.Length - 1; i >= 0; --i)
            {
                var testString = processedTokens[i];
                var match = false;
                for (var j = 0; !match && j < processedTokens.Length; ++j)
                {
                    if (j != i)
                    {
                        var token = processedTokens[j];
                        match |= token.IndexOf(testString) != -1;
                    }
                }

                if (match)
                {
                    // Swap with last and remove
                    processedTokens[i] = processedTokens[processedTokens.Length - 1];
                    processedTokens.RemoveAt(processedTokens.Length - 1);
                }
            }

            // Sort filters by length (longest first)
            // We are doing this because filters are additive and therefore longer filters are less likely to match making it faster to discard candidates
            processedTokens.Sort(new NameLengthComparer());
        }

        struct NameLengthComparer : IComparer<FixedString64Bytes>
        {
            public int Compare(FixedString64Bytes lhs, FixedString64Bytes rhs)
            {
                if (lhs.utf8LengthInBytes == rhs.utf8LengthInBytes)
                    return 0;
                return lhs.utf8LengthInBytes < rhs.utf8LengthInBytes ? 1 : -1;
            }
        }

        /// <summary>
        /// @TODO convert this to an enumerator which can be time-sliced.
        /// </summary>
        public NativeBitArray Apply(HierarchyNodeStore.Immutable nodes, Allocator allocator)
        {
            var mask = new NativeBitArray(nodes.Count, allocator);
            mask.SetBits(0, true, mask.Length);

            m_HierarchySearch.ApplyEntityIndexFilter(nodes, m_EntityIndex, mask);
            m_HierarchySearch.ApplyEntityQueryFilter(nodes, m_QueryResult.EntityQueryDesc, EntityQueryModifiers, mask, allocator);

            m_HierarchySearch.ApplyNameFilter(nodes, m_Tokens, mask);
            m_HierarchySearch.ApplyIncludeSubSceneFilter(nodes, mask);
            if (m_Kind != NodeKind.None)
                m_HierarchySearch.ApplyNodeKindFilter(nodes, m_Kind, mask);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                m_HierarchySearch.ApplyPrefabStageFilter(nodes, mask);

            return mask;
        }
    }
}
