#if PUG_MOD_SDK
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PugMod
{
	public class DefineProcessor : AssetPostprocessor
	{
		private const string DEFINE = "USE_PUG_OTHER";
		private const string DLL_NAME = "Pug.Other.dll";

		static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			bool dllRemoved = deletedAssets.Any(p => Path.GetFileName(p).Equals(DLL_NAME, System.StringComparison.OrdinalIgnoreCase));
			bool dllAdded   = importedAssets.Any(p => Path.GetFileName(p).Equals(DLL_NAME, System.StringComparison.OrdinalIgnoreCase));

			if (dllRemoved)
			{
				SetDefine(false);
			}
			else if (dllAdded)
			{
				SetDefine(true);
			}
		}

		[InitializeOnLoadMethod]
		private static void SyncDefineOnLoad()
		{
			bool dllPresent = AssetDatabase
				.FindAssets(Path.GetFileNameWithoutExtension(DLL_NAME))
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Any(path => Path.GetFileName(path).Equals(DLL_NAME, System.StringComparison.OrdinalIgnoreCase));

			SetDefine(dllPresent);
		}

		private static void SetDefine(bool enable)
		{
			var buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
				EditorUserBuildSettings.selectedBuildTargetGroup);

			var current = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
			var defines = new HashSet<string>(
				current.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));

			bool changed = enable ? defines.Add(DEFINE) : defines.Remove(DEFINE);

			if (changed)
			{
				PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", defines));
			}
		}
	}
}
#endif
