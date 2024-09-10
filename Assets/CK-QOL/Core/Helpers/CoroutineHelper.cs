using System.Collections;
using UnityEngine;

namespace CK_QOL.Core.Helpers
{
	/// <summary>
	///		Provides a helper class for running coroutines outside a MonoBehaviour context.
	///		This class ensures that there is always a MonoBehaviour available to handle coroutine operations.
	/// </summary>
	public class CoroutineHelper : MonoBehaviour
	{
		private static CoroutineHelper _instance;

		/// <summary>
		///		Gets the singleton instance of the CoroutineHelper.
		///		If no instance exists, it creates a new GameObject with this component attached.
		/// </summary>
		public static CoroutineHelper Instance
		{
			get
			{
				if (_instance != null)
				{
					return _instance;
				}

				var gameObject = new GameObject($"{ModSettings.ShortName}-{nameof(CoroutineHelper)}");
				_instance = gameObject.AddComponent<CoroutineHelper>();
                
				DontDestroyOnLoad(gameObject);

				return _instance;
			}
		}

		/// <summary>
		///		Runs the specified coroutine using Unity's StartCoroutine method.
		/// </summary>
		/// <param name="coroutine">The IEnumerator representing the coroutine to run.</param>
		public void RunCoroutine(IEnumerator coroutine)
		{
			StartCoroutine(coroutine);
		}
	}
}