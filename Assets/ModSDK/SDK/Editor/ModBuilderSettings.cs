using System;
using System.Collections.Generic;
using System.IO;
using PugMod;
using UnityEditor;
using UnityEngine;

public class ModBuilderSettings : ScriptableObject
{
	public ModMetadata metadata = new ModMetadata
	{
		guid = Guid.NewGuid().ToString("N"),
		name = "MyMod",
	};
	
	public string modPath = "Assets/Mod";

	public bool forceReimport = true;
	public bool buildBundles = true;
	public bool cacheBundles = false;
	public bool buildLinux = true;
	
	[HideInInspector]
	public List<ModAsset> assets;
	[HideInInspector]
	public bool lastBuildLinux = false;
	
	[Serializable]
	public struct ModAsset
	{
		public string path;
		public string hash;
	}
	
	private void OnValidate()
	{
		if (string.IsNullOrEmpty(modPath))
		{
			var path = AssetDatabase.GetAssetPath(this);
			modPath = Path.GetDirectoryName(path);
		}
	}
}