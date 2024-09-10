using UnityEngine;

namespace CK_QOL.Core
{
	internal static class ModLogger
	{
		private const string Prefix = "[" + ModSettings.ShortName + "]";

		internal static void Info(object message)
		{
			Debug.Log($"{Prefix} {message}");
		}
		
		internal static void Warn(object message)
		{
			Debug.LogWarning($"{Prefix} {message}");
		}
		
		internal static void Error(object message)
		{
			Debug.LogError($"{Prefix} {message}");
		}
	}
}