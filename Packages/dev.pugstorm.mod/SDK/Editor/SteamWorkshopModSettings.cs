using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace PugMod
{
	public class SteamWorkshopModSettings : ScriptableObject
	{
		[ReadOnly][SerializeField] public ulong fileId;
		public ulong _fileId => fileId;
		[ReadOnly][SerializeField] public string modOwner;
		public string _modOwner => modOwner;

		// The mod's internal name, matching ModMetadata.name. The Steam Workshop tab
		// looks up settings by this field, so it must not hold a display title.
		public string modName;
		// The Workshop title, which may differ from modName. Empty in settings
		// written before this field existed.
		public string title;
		public string selectedPath;
		public List<string> tags = new();

		//internal void Change(string ModOwner) if we want to serialize it but don't want it visible in inspector
		//{
		//	modOwner = ModOwner;
		//}
	}
}
