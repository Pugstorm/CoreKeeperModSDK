using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Editor.Bridge
{
    static class PrefabUtilityBridge
    {
        public const string kDummyPrefabStageRootObjectName = PrefabUtility.kDummyPrefabStageRootObjectName;

        public static bool IsGameObjectThePrefabRootInAnyPrefabStage(GameObject gameObject) => PrefabStageUtility.IsGameObjectThePrefabRootInAnyPrefabStage(gameObject);

        public static string GetAssetPathOfSourcePrefab([NotNull] UnityObject prefabPart, out bool isRoot)
        {
            var sourcePrefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabPart);

            isRoot = PrefabUtility.IsObjectOnRootInAsset(prefabPart, sourcePrefabAssetPath);

            return sourcePrefabAssetPath;
        }

        // Kept around for debugging. You have no idea how useful that method is!
        public static void DumpPrefabInfo(UnityObject prefab)
        {
            var prefabInstanceID = prefab.GetInstanceID();
            var prefabAssetHandle = PrefabUtility.GetPrefabAssetHandle(prefab);
            var prefabAssetPath = AssetDatabase.GetAssetPath(prefabInstanceID);
            var prefabAsGameObject = prefab as GameObject;

            var objFromSource = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            var objFromSourceID = objFromSource ? objFromSource.GetInstanceID() : 0;
            var objFromOrigSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
            var objFromOrigSourceID = objFromOrigSource ? objFromOrigSource.GetInstanceID() : 0;
            var objFromSourcePath = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(prefab, prefabAssetPath);
            var objFromSourcePathID = objFromSourcePath ? objFromSourcePath.GetInstanceID() : 0;
            var objFromSourceInAsset = PrefabUtility.GetCorrespondingObjectFromSourceInAsset(prefab, prefabAssetHandle);
            var objFromSourceInAssetID = objFromSourceInAsset ? objFromSourceInAsset.GetInstanceID() : 0;

            var prefabRootGameObject = PrefabUtility.GetPrefabAssetRootGameObject(prefab);
            var prefabRootGameObjectID = prefabRootGameObject ? prefabRootGameObject.GetInstanceID() : 0;
            var originalSourceOrVariantRoot = PrefabUtility.GetOriginalSourceOrVariantRoot(prefab);
            var originalSourceOrVariantRootID = originalSourceOrVariantRoot ? originalSourceOrVariantRoot.GetInstanceID() : 0;

            var nearestPrefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(prefab);
            var nearestPrefabRootID = nearestPrefabRoot ? nearestPrefabRoot.GetInstanceID() : 0;

            Debug.Log("Prefab state:\n" +
                              $"    IsPartOfAnyPrefab = {PrefabUtility.IsPartOfAnyPrefab(prefab)}\n" +
                              $"    IsPartOfPrefabAsset = {PrefabUtility.IsPartOfPrefabAsset(prefab)}\n" +
                              $"    IsPartOfPrefabInstance = {PrefabUtility.IsPartOfPrefabInstance(prefab)}\n" +
                              $"    IsAnyPrefabInstanceRoot = {prefabAsGameObject && PrefabUtility.IsAnyPrefabInstanceRoot(prefabAsGameObject)}\n" +
                              $"    IsOutermostPrefabInstanceRoot = {prefabAsGameObject && PrefabUtility.IsOutermostPrefabInstanceRoot(prefabAsGameObject)}\n" +
                              $"    IsObjectOnRootInAsset = {PrefabUtility.IsObjectOnRootInAsset(prefab, prefabAssetPath)}\n" +
                              $"    IsPartOfPrefabThatCanBeAppliedTo = {PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(prefab)}\n" +
                              $"    GetPrefabAssetType = {PrefabUtility.GetPrefabAssetType(prefab)}\n" +
                              $"    InstanceID = {prefabInstanceID}\n" +
                              $"    GetPrefabAssetRootGameObject.InstanceID = {prefabRootGameObjectID}\n" +
                              $"    GetCorrespondingObjectFromSource.InstanceID = {objFromSourceID}\n" +
                              $"    GetCorrespondingObjectFromOriginalSource.InstanceID = {objFromOrigSourceID}\n" +
                              $"    GetCorrespondingObjectFromSourceAtPath.InstanceID = {objFromSourcePathID}\n" +
                              $"    GetCorrespondingObjectFromSourceInAsset.InstanceID = {objFromSourceInAssetID}\n" +
                              $"    GetOriginalSourceOrVariantRoot.InstanceID = {originalSourceOrVariantRootID}\n" +
                              $"    GetNearestPrefabInstanceRoot.InstanceID = {nearestPrefabRootID}\n" +
                              $"    AssetPath = {prefabAssetPath}\n" +
                              $"    GetPrefabAssetRootGameObject.AssetPath = {AssetDatabase.GetAssetPath(prefabRootGameObjectID)}\n" +
                              $"    GetCorrespondingObjectFromSourceAtPath.AssetPath = {AssetDatabase.GetAssetPath(objFromSourcePathID)}\n" +
                              $"    GetCorrespondingObjectFromSourceInAsset.AssetPath = {AssetDatabase.GetAssetPath(objFromSourceInAssetID)}\n" +
                              $"    GetAssetPathOfSourcePrefab = {PrefabUtility.GetAssetPathOfSourcePrefab(prefab)}\n" +
                              $"    GetPrefabAssetPathOfNearestInstanceRoot = {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab)}\n" +
                              "");
        }
    }
}
