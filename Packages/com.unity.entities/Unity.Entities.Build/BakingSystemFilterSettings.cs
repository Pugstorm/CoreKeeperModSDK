using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace Unity.Entities.Build
{
#if UNITY_EDITOR
    /// <summary>
    /// Holds the set of assemblies which are to be excluded during the baking process.
    /// </summary>
    /// <remarks>
    /// The <see cref="Unity.Entities.Baker{TAuthoringType}"/> types defined in excluded assemblies are ignored during the baking process.
    /// </remarks>
    [Serializable]
    public sealed class BakingSystemFilterSettings
    {
        HashSet<Assembly> m_ExcludedDomainAssemblies;

        // this must be initialized to true, so that when properties does a transfer
        // and updates the List<string> property, we get a chance to tell m_ConversionTypeCache
        // about the change.
        bool m_IsDirty = true;

        /// <summary>
        /// The list of assemblies containing bakers which are going to be excluded during the baking process.
        /// </summary>
        [SerializeField]
        public List<UnityEngine.LazyLoadReference<UnityEditorInternal.AssemblyDefinitionAsset>> ExcludedBakingSystemAssemblies =
            new List<UnityEngine.LazyLoadReference<UnityEditorInternal.AssemblyDefinitionAsset>>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BakingSystemFilterSettings() {}

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="excludedAssemblyDefinitionNames">
        /// The names of assemblies containing bakers which are going to be excluded during the baking process.
        /// </param>
        public BakingSystemFilterSettings(params string[] excludedAssemblyDefinitionNames)
        {
            foreach (var name in excludedAssemblyDefinitionNames)
            {
                var asset = FindAssemblyDefinitionAssetByName(name);
                if (asset != null && asset)
                {
                    ExcludedBakingSystemAssemblies.Add(asset);
                }
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="excludedAssemblyDefinitionAssets">
        /// The list of assemblies containing bakers which are going to be excluded during the baking process.
        /// </param>
        public BakingSystemFilterSettings(params UnityEditorInternal.AssemblyDefinitionAsset[] excludedAssemblyDefinitionAssets)
        {
            foreach (var asset in excludedAssemblyDefinitionAssets)
            {
                if (asset != null && asset)
                {
                    ExcludedBakingSystemAssemblies.Add(asset);
                }
            }
        }

        internal UnityEditorInternal.AssemblyDefinitionAsset FindAssemblyDefinitionAssetByName(string name)
        {
            var assetPath = UnityEditor.AssetDatabase.FindAssets($"t: asmdef {name}")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(assetPath);
        }

        //todo: add this attribute to GetSystemAttributes, and then stop using reflection here
        internal bool ShouldRunBakingSystem(Type type)
        {
            UpdateIfDirty();
            if (m_ExcludedDomainAssemblies == null)
                return true;

            if (type.GetCustomAttribute<AlwaysIncludeBakingSystemAttribute>() != null)
                return true;

            return !m_ExcludedDomainAssemblies.Contains(type.Assembly);
        }

        /// <summary>
        /// Checks if an assembly is excluded during the baking process.
        /// </summary>
        /// <param name="assembly">The assembly to check.</param>
        /// <returns>Returns true if the assembly is excluded, otherwise returns false.</returns>
        public bool IsAssemblyExcluded(Assembly assembly)
        {
            UpdateIfDirty();
            if (m_ExcludedDomainAssemblies == null)
                return false;

            return m_ExcludedDomainAssemblies.Contains(assembly);
        }

        // TODO: DOTS-7396 - Apply filter to baking systems
        internal void SetDirty()
        {
            m_IsDirty = true;
        }

        void UpdateIfDirty()
        {
            if (!m_IsDirty)
                return;

            if (ExcludedBakingSystemAssemblies.Count == 0)
            {
                m_ExcludedDomainAssemblies = null;
                return;
            }

            m_ExcludedDomainAssemblies = new HashSet<Assembly>();

            var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var excludedAssembly in ExcludedBakingSystemAssemblies.Select(lazy => lazy.asset))
            {
                if (excludedAssembly != null)
                {
                    var asm = domainAssemblies.FirstOrDefault(s => s.GetName().Name == excludedAssembly.name);
                    if (asm != null)
                        m_ExcludedDomainAssemblies.Add(asm);
                }
            }
            m_IsDirty = false;
        }
    }
#endif
}
