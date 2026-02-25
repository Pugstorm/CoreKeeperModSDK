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

		public string modName;
		public string selectedPath;
		public List<string> tags = new();

		//internal void Change(string ModOwner) if we want to serialize it but don't want it visible in inspector
		//{
		//	modOwner = ModOwner;
		//}
	}
}
