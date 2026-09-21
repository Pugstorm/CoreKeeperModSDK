#if PUG_MOD_SDK && USE_PUG_OTHER
using System.Collections.Generic;
using UnityEditor;
using PugMod;
using System;
using UnityEditor.SceneManagement;
using System.Linq;

public class ModBuilderCustomScenesProcessor : IPugModBuilderProcessor
{
    public void Execute(ModBuilderSettings settings, string installDirectory, List<string> assetPaths)
    {
        string normalizedModPath = settings.modPath.Replace('\\', '/').TrimEnd('/');

        var sceneDataBlocks = new List<CustomSceneDataBlock>();

        foreach (var sceneDataBlock in ScriptableDataEditorUtility.GetCachedDataBlocks<CustomSceneDataBlock>())
        {
            if (!sceneDataBlock.sceneReference.IsAssigned())
            {
                continue;
            }
            string assetPath = AssetDatabase.GetAssetPath(sceneDataBlock)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            bool isInsideMod = assetPath.Equals(normalizedModPath, StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(normalizedModPath + "/", StringComparison.OrdinalIgnoreCase);

            if (isInsideMod)
            {
                sceneDataBlocks.Add(sceneDataBlock);
            }
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        bool hasActiveScene = activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path);

        if (sceneDataBlocks.Count > 0 && hasActiveScene)
        {
            EditorSceneManager.SaveOpenScenes();

            bool[] scenesToInclude = new bool[sceneDataBlocks.Count];
            Array.Fill(scenesToInclude, true);
            var sceneNames = string.Join(", ", sceneDataBlocks.Select(s => s.sceneReference.AssetName()));

            bool userWantToProcessScene = EditorUtility.DisplayDialog("Custom Scene Processing", $"Would you like to process the scene {sceneNames} belonging to the mod: {settings.name}? " +
                $"Cancelling will not process the scene, but will proceed with the rest of the mod building.", "OK");

            if (!userWantToProcessScene)
            {
                assetPaths.RemoveAll(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
                return;
            }

            CustomSceneProcessorUtility.ProcessScenes(sceneDataBlocks, scenesToInclude, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var currentGuids = AssetDatabase.FindAssets("t:Object", new[] { settings.modPath });
            var currentPaths = currentGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path)
                    && !AssetDatabase.IsValidFolder(path)
                    && !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(ModBuilderSettings)
                    && AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(PugMod.ModIO.ModSettings)
                    && AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(SteamWorkshopModSettings))
                .ToList();

            assetPaths.Clear();
            assetPaths.AddRange(currentPaths);
        }
    }
}
#endif
