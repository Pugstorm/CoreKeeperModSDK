using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities
{
    internal class IncrementalBakingChangeTracker : IDisposable
    {
        internal NativeList<int> DeletedInstanceIds;
        internal NativeParallelHashSet<int> ChangedInstanceIds;
        internal NativeParallelHashSet<int> BakeHierarchyInstanceIds;
        internal NativeParallelHashSet<int> ForceBakeHierarchyInstanceIds;
        internal NativeParallelHashMap<int, int> ParentChangeInstanceIds;
        internal NativeList<int> ChangedAssets;
        internal NativeList<int> DeletedAssets;
        internal readonly HashSet<Component> ComponentChanges;
        private readonly List<Component> ValidComponents;
        internal NativeList<int> ParentWithChildrenOrderChangedInstanceIds;
        internal bool LightBakingChanged;

        public IncrementalBakingChangeTracker()
        {
            DeletedInstanceIds = new NativeList<int>(Allocator.Persistent);
            ChangedInstanceIds = new NativeParallelHashSet<int>(10, Allocator.Persistent);
            BakeHierarchyInstanceIds = new NativeParallelHashSet<int>(10, Allocator.Persistent);
            ForceBakeHierarchyInstanceIds = new NativeParallelHashSet<int>(10, Allocator.Persistent);
            ParentChangeInstanceIds = new NativeParallelHashMap<int, int>(10, Allocator.Persistent);
            ChangedAssets = new NativeList<int>(Allocator.Persistent);
            DeletedAssets = new NativeList<int>(Allocator.Persistent);
            ComponentChanges = new HashSet<Component>();
            ValidComponents = new List<Component>();
            ParentWithChildrenOrderChangedInstanceIds = new NativeList<int>(Allocator.Persistent);
            LightBakingChanged = false;
        }

        internal void Clear()
        {
            DeletedInstanceIds.Clear();
            ChangedInstanceIds.Clear();
            BakeHierarchyInstanceIds.Clear();
            ForceBakeHierarchyInstanceIds.Clear();
            ParentChangeInstanceIds.Clear();
            ChangedAssets.Clear();
            DeletedAssets.Clear();
            ComponentChanges.Clear();
            ValidComponents.Clear();
            ParentWithChildrenOrderChangedInstanceIds.Clear();
            LightBakingChanged = false;
        }

        internal bool HasAnyChanges()
        {
            return DeletedInstanceIds.Length > 0 ||
                !ChangedInstanceIds.IsEmpty ||
                !BakeHierarchyInstanceIds.IsEmpty ||
                !ForceBakeHierarchyInstanceIds.IsEmpty ||
                ComponentChanges.Count > 0 ||
                !ParentChangeInstanceIds.IsEmpty ||
                ChangedAssets.Length > 0 ||
                DeletedAssets.Length > 0 ||
                ParentWithChildrenOrderChangedInstanceIds.Length > 0 ||
                LightBakingChanged;
        }

        internal void FillBatch(ref IncrementalBakingBatch batch)
        {
            batch.DeletedInstanceIds = DeletedInstanceIds.AsArray();
            batch.ChangedInstanceIds = ChangedInstanceIds.ToNativeArray(Allocator.Temp);
            batch.BakeHierarchyInstanceIds = BakeHierarchyInstanceIds.ToNativeArray(Allocator.Temp);
            batch.ForceBakeHierarchyInstanceIds = ForceBakeHierarchyInstanceIds.ToNativeArray(Allocator.Temp);
            batch.ParentChangeInstanceIds = ParentChangeInstanceIds;
            batch.ChangedAssets = ChangedAssets.AsArray();
            batch.DeletedAssets = DeletedAssets.AsArray();
            batch.ChangedComponents = ValidComponents;
            batch.ParentWithChildrenOrderChangedInstanceIds = ParentWithChildrenOrderChangedInstanceIds.AsArray();
            // We don't need RecreateInstanceIds unless an previously baked entity has been deleted
            batch.RecreateInstanceIds = default;
            batch.LightBakingChanged = LightBakingChanged;
            ValidComponents.AddRange(ComponentChanges);
        }

        public void Dispose()
        {
            if (DeletedInstanceIds.IsCreated)
                DeletedInstanceIds.Dispose();
            if (ChangedInstanceIds.IsCreated)
                ChangedInstanceIds.Dispose();
            if (BakeHierarchyInstanceIds.IsCreated)
                BakeHierarchyInstanceIds.Dispose();
            if (ForceBakeHierarchyInstanceIds.IsCreated)
                ForceBakeHierarchyInstanceIds.Dispose();
            if (ParentChangeInstanceIds.IsCreated)
                ParentChangeInstanceIds.Dispose();
            if (ChangedAssets.IsCreated)
                ChangedAssets.Dispose();
            if (DeletedAssets.IsCreated)
                DeletedAssets.Dispose();
            if (ParentWithChildrenOrderChangedInstanceIds.IsCreated)
                ParentWithChildrenOrderChangedInstanceIds.Dispose();
        }

        public void MarkAssetChanged(int assetInstanceId) => ChangedAssets.Add(assetInstanceId);

        public void MarkRemoved(int instanceId)
        {
            BakeHierarchyInstanceIds.Remove(instanceId);
            ForceBakeHierarchyInstanceIds.Remove(instanceId);
            ChangedInstanceIds.Remove(instanceId);
            ParentChangeInstanceIds.Remove(instanceId);
            DeletedInstanceIds.Add(instanceId);
        }

        public void MarkParentChanged(int instanceId, int parentInstanceId)
        {
            if (!ParentChangeInstanceIds.TryAdd(instanceId, parentInstanceId))
            {
                ParentChangeInstanceIds.Remove(instanceId);
                ParentChangeInstanceIds.Add(instanceId, parentInstanceId);
            }
        }

        public void MarkComponentChanged(Component c) => ComponentChanges.Add(c);
        public void MarkBakeHierarchy(int instanceId) => BakeHierarchyInstanceIds.Add(instanceId);
        public void MarkForceBakeHierarchy(int instanceId) => ForceBakeHierarchyInstanceIds.Add(instanceId);
        public void MarkChanged(int instanceId) => ChangedInstanceIds.Add(instanceId);

        public void MarkChildrenOrderChange(int instanceId) =>
            ParentWithChildrenOrderChangedInstanceIds.Add(instanceId);

        public void MarkLightBakingChanged() => LightBakingChanged = true;
    }

    /// <summary>
    /// Represents a fine-grained description of changes that happened since the last conversion.
    /// </summary>
    internal struct IncrementalBakingBatch : IDisposable
    {
        /// <summary>
        /// Instance IDs of all GameObjects that were deleted.
        /// Note that this can overlap with any of the other collections.
        /// </summary>
        public NativeArray<int> DeletedInstanceIds;

        /// <summary>
        /// Instance IDs of all GameObjects that were changed.
        /// /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<int> ChangedInstanceIds;

        /// <summary>
        /// Instance IDs of all GameObjects that should have the entire hierarchy below them reconverted.
        /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<int> BakeHierarchyInstanceIds;

        /// <summary>
        /// Instance IDs of all GameObjects that should have the entire hierarchy below them reconverted.
        /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<int> ForceBakeHierarchyInstanceIds;

        /// <summary>
        /// Instance IDs of all GameObjects that have lost their Primary Entity
        /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<int> RecreateInstanceIds;

        /// <summary>
        /// Maps instance IDs of GameObjects to the instance ID of their last recorded parent if the parenting changed.
        /// Note that this might included instance IDs of destroyed GameObjects on either side.
        /// </summary>
        public NativeParallelHashMap<int, int> ParentChangeInstanceIds;

        /// <summary>
        /// Contains the instance IDs of all assets that were changed since the last conversion.
        /// </summary>
        public NativeArray<int> ChangedAssets;

        /// <summary>
        /// Contains the GUIDs of all assets that were deleted since the last conversion.
        /// </summary>
        public NativeArray<int> DeletedAssets;

        /// <summary>
        /// Contains a list of all components that were changed since the last conversion. Note that the components
        /// might have been destroyed in the mean time.
        /// </summary>
        public List<Component> ChangedComponents;

        /// <summary>
        /// Contains all the instance ids of the parents with children being reordered
        /// </summary>
        public NativeArray<int> ParentWithChildrenOrderChangedInstanceIds;

        /// <summary>
        /// True if the lights have been baked, meaning that the components that depend on light mapping should be updated
        /// </summary>
        public bool LightBakingChanged;

        public void Dispose()
        {
            DeletedInstanceIds.Dispose();
            ChangedInstanceIds.Dispose();
            BakeHierarchyInstanceIds.Dispose();
            ForceBakeHierarchyInstanceIds.Dispose();
            ParentChangeInstanceIds.Dispose();
            ChangedAssets.Dispose();
            DeletedAssets.Dispose();
            ParentWithChildrenOrderChangedInstanceIds.Dispose();
            if (RecreateInstanceIds.IsCreated)
                RecreateInstanceIds.Dispose();
        }
#if UNITY_EDITOR
        internal string FormatSummary()
        {
            var sb = new StringBuilder();
            FormatSummary(sb);
            return sb.ToString();
        }

        internal void FormatSummary(StringBuilder sb)
        {
            sb.AppendLine(nameof(IncrementalBakingBatch));

            sb.Append(nameof(LightBakingChanged));
            sb.Append(": ");
            sb.AppendLine(LightBakingChanged.ToString());

            PrintOut(sb, nameof(DeletedInstanceIds), DeletedInstanceIds);
            PrintOut(sb, nameof(ChangedInstanceIds), ChangedInstanceIds);
            PrintOut(sb, nameof(BakeHierarchyInstanceIds), BakeHierarchyInstanceIds);
            PrintOut(sb, nameof(ForceBakeHierarchyInstanceIds), ForceBakeHierarchyInstanceIds);
            if (RecreateInstanceIds.IsCreated)
                PrintOut(sb, nameof(RecreateInstanceIds), RecreateInstanceIds);
            PrintOut(sb, nameof(ChangedAssets), ChangedAssets);
            PrintOut(sb, nameof(DeletedAssets), DeletedAssets);

            if (ChangedComponents.Count > 0)
            {
                sb.Append(nameof(ChangedComponents));
                sb.Append(": ");
                sb.Append(ChangedComponents.Count);
                sb.AppendLine();
                foreach (var c in ChangedComponents)
                {
                    sb.Append('\t');
                    sb.Append(c.ToString());
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            if (!ParentChangeInstanceIds.IsEmpty)
            {
                sb.Append(nameof(ParentChangeInstanceIds));
                sb.Append(": ");
                sb.Append(ParentChangeInstanceIds.Count());
                sb.AppendLine();
                foreach (var kvp in ParentChangeInstanceIds)
                {
                    sb.Append('\t');
                    sb.Append(kvp.Key);
                    sb.Append(" (");
                    {
                        var obj = EditorUtility.InstanceIDToObject(kvp.Key);
                        if (obj == null)
                            sb.Append("null");
                        else
                            sb.Append(obj.name);
                    }
                    sb.Append(") reparented to ");
                    sb.Append(kvp.Value);
                    sb.Append(" (");
                    {
                        var obj = EditorUtility.InstanceIDToObject(kvp.Value);
                        if (obj == null)
                            sb.Append("null");
                        else
                            sb.Append(obj.name);
                    }
                    sb.AppendLine(")");
                }
            }
        }

        static void PrintOut(StringBuilder sb, string name, NativeArray<int> instanceIds)
        {
            if (instanceIds.Length == 0)
                return;
            sb.Append(name);
            sb.Append(": ");
            sb.Append(instanceIds.Length);
            sb.AppendLine();
            for (int i = 0; i < instanceIds.Length; i++)
            {
                sb.Append('\t');
                sb.Append(instanceIds[i]);
                sb.Append(" - ");
                var obj = EditorUtility.InstanceIDToObject(instanceIds[i]);
                if (obj == null)
                    sb.AppendLine("(null)");
                else
                    sb.AppendLine(obj.name);
            }

            sb.AppendLine();
        }

#endif
    }
}
