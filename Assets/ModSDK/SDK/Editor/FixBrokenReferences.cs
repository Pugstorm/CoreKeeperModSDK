#if PUG_MOD_SDK
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

/*
SpriteInstancing changed namespace to Pug.Sprite:
SpriteObject
179130730 -> 1908045241

SpriteAsset
232162479 -> -217761678

SpriteAssetManifest
-1723848245 -> 1876717734

SpriteAssetSkin
-218616042 -> 1297384836

TransformAnimation
1101876616 -> -1781689278

SpriteObjectMask (new)
N/A -> -1243220692
 */

public class FixBrokenReferences : AssetPostprocessor
{
	private static Dictionary<string, string> idMap = new Dictionary<string, string>
	{
		{ "fileID: 179130730", "fileID: 1908045241" },
		{ "fileID: 232162479", "fileID: -217761678" },
		{ "fileID: -1723848245", "fileID: 1876717734" },
		{ "fileID: -218616042", "fileID: 1297384836" },
		{ "fileID: 1101876616", "fileID: -1781689278" },
	};

	private void OnPreprocessAsset()
	{
		if (!assetPath.EndsWith(".prefab") && !assetPath.EndsWith(".asset")) return;

		string text = File.ReadAllText(assetPath);
		bool modified = false;

		foreach (var kvp in idMap)
		{
			if (text.Contains(kvp.Key))
			{
				Debug.Log($"Replacing {kvp.Key} with {kvp.Value} in {assetPath}");
				text = text.Replace(kvp.Key, kvp.Value);
				modified = true;
			}
		}

		if (modified)
		{
			File.WriteAllText(assetPath, text);
			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
		}
	}
}
#endif