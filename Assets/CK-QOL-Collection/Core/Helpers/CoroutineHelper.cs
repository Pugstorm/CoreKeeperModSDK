using System.Collections;
using UnityEngine;

namespace CK_QOL_Collection.Core.Helpers
{
	/// <summary>
	///		Provides a helper class for running coroutines outside of a MonoBehaviour context.
	///		This class ensures that there is always a MonoBehaviour available to handle coroutine operations.
	/// </summary>
	public class CoroutineHelper : MonoBehaviour
	{
		// Singleton instance of the CoroutineHelper
		private static CoroutineHelper instance;

		/// <summary>
		///		Gets the singleton instance of the CoroutineHelper.
		///		If no instance exists, it creates a new GameObject with this component attached.
		/// </summary>
		public static CoroutineHelper Instance
		{
			get
			{
				// If the instance already exists, return it
				if (instance != null)
				{
					return instance;
				}

				// Otherwise, create a new GameObject and attach this component to it
				var gameObject = new GameObject("CK-QOL-CoroutineHelper");
				instance = gameObject.AddComponent<CoroutineHelper>();
                
				// Ensure that the GameObject persists across scene changes
				DontDestroyOnLoad(gameObject);

				return instance;
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