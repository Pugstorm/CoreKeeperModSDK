using System;
using System.Collections.Generic;
using PugMod;
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
	public bool buildLinux = false;
	
	public bool buildBurst = true;

	private void OnValidate()
	{
		if (string.IsNullOrEmpty(modPath))
		{
			var path = AssetDatabase.GetAssetPath(this);
			modPath = Path.GetDirectoryName(path);
		}
	}
}