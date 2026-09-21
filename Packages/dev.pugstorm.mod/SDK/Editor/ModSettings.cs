using ModIO;
using System.Collections.Generic;
using UnityEngine;

namespace PugMod.ModIO
{
	public class ModSettings : ScriptableObject
	{
		public long modId;
		public ModBuilderSettings modSettings;
		public Texture2D logo;
		public string title;
		public string summary;
		public bool visible = true;
		public List<string> tags = new List<string>();
	}
}