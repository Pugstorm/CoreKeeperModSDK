#if USE_PUG_OTHER
using UnityEditor;
using UnityEngine;

public static class MigrateObjectAuthoring
{
	private static readonly DataBlockAddress POOL_8X_ADDRESS = new("816ac074-61d8-f6c4-0988-9c393e342e00");

	[MenuItem("PugMod/Prefab Migration/Create DataBlocks for ObjectAuthoring prefabs")]
	public static void MigrateObjectAuthoringPrefabs()
	{
		int processedPrefabCount = 0;
		int graphicalDataBlocksCreated = 0;
		int authoringDataBlocksCreated = 0;

		foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);

			if (path.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

			if (prefab == null)
			{
				continue;
			}
			
			if (!prefab.TryGetComponent<ObjectAuthoring>(out var objectAuthoring))
			{
				continue;
			}

			string prefabDirectory = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
			string contextDirectory = FindNearestScriptableDataDirectory(prefabDirectory);

			if (contextDirectory != null)
			{
				if (ScriptableDataEditorUtility.TryGetContextOfDirectory(contextDirectory, out var scriptableDataDirectory))
				{
					ScriptableDataEditorUtility.SetContext(scriptableDataDirectory);
				}
			}

			bool modified = false;

			if (objectAuthoring.graphicalPrefab != null && !objectAuthoring.graphicalRef.hasAddress)
			{
				var graphicalName = objectAuthoring.graphicalPrefab.name;

				if (TryGetOrCreateDataBlock<GraphicalObjectDataBlock>(graphicalName, out var graphicalDataBlock))
				{
					graphicalDataBlock.prefab = objectAuthoring.graphicalPrefab;
					graphicalDataBlock.poolParams = new DataBlockRef<PoolParameterDataBlock>(POOL_8X_ADDRESS);
					EditorUtility.SetDirty(graphicalDataBlock);

					using var so = new SerializedObject(objectAuthoring);
					so.FindProperty("graphicalRef").SetDataBlock(graphicalDataBlock);
					so.ApplyModifiedProperties();

					graphicalDataBlocksCreated++;
					modified = true;
					Debug.Log($"Created {nameof(GraphicalObjectDataBlock)} '{graphicalName}' for '{prefab.name}' (assigned 8x pool).", prefab);
				}
			}

			if (!objectAuthoring.authoringRef.hasAddress)
			{
				var authoringName = objectAuthoring.variation > 0
					? $"{prefab.name} (v{objectAuthoring.variation})"
					: prefab.name;

				if (TryGetOrCreateDataBlock<EntityAuthoringDataBlock>(authoringName, out var authoringDataBlock))
				{
					authoringDataBlock.prefab = prefab;
					EditorUtility.SetDirty(authoringDataBlock);

					using var so = new SerializedObject(objectAuthoring);
					so.FindProperty("authoringRef").SetDataBlock(authoringDataBlock);
					so.ApplyModifiedProperties();

					authoringDataBlocksCreated++;
					modified = true;
					Debug.Log($"Created {nameof(EntityAuthoringDataBlock)} '{authoringName}' for '{prefab.name}'.", prefab);
				}
			}

			if (modified)
			{
				EditorUtility.SetDirty(objectAuthoring);
				EditorUtility.SetDirty(prefab);
				PrefabUtility.SavePrefabAsset(prefab);
				processedPrefabCount++;
			}
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		string summary = $"Done. Processed {processedPrefabCount} prefabs.\n" +
			$"Created {graphicalDataBlocksCreated} GraphicalObjectDataBlocks, {authoringDataBlocksCreated} EntityAuthoringDataBlocks.\n\n" +
			$"All new GraphicalObjectDataBlocks have been assigned the 8x pool size PoolParameterDataBlock by default.";
		Debug.Log(summary);

		EditorUtility.DisplayDialog(
			"Migration Complete",
			$"{summary}\n\n" +
			"To change pool settings for individual objects, select the GraphicalObjectDataBlock asset " +
			"and modify the 'Pool Params' field in the Inspector or Scriptable Data Editor Window.",
			"OK");
	}

	private static string FindNearestScriptableDataDirectory(string startPath)
	{
		string current = startPath;

		while (!string.IsNullOrEmpty(current) && current != "Assets")
		{
			var guids = AssetDatabase.FindAssets("t:ScriptableDataDirectory", new[] { current });

			if (guids.Length > 0)
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
				return System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
			}

			current = System.IO.Path.GetDirectoryName(current)?.Replace('\\', '/');
		}

		return null;
	}

	private static bool TryGetOrCreateDataBlock<T>(string name, out T dataBlock) where T : ScriptableDataBlock
	{
		if (!ScriptableDataEditorUtility.TryAddDataBlock(
				subDirectory: null,
				name: name,
				refreshAssetDatabase: false,
				dataBlock: out dataBlock))
		{
			ScriptableDataEditorUtility.TryFindDataBlock(name, out dataBlock);
		}

		if (dataBlock != null)
		{
			return true;
		}

		Debug.LogError($"Failed to create or find {typeof(T).Name} with name '{name}'.");
		return false;
	}
}
#endif