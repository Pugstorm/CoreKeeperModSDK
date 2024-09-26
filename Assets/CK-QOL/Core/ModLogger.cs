using UnityEngine;

namespace CK_QOL.Core
{
	/// <summary>
	///     Provides logging functionalities for the mod, including informational, warning, and error logs.
	///     This class wraps Unity's <see cref="Debug" /> logging methods and adds a custom prefix to all log messages.
	/// </summary>
	/// <remarks>
	///     The logging methods automatically prepend the mod's short name (from <see cref="ModSettings.ShortName" />) to each
	///     log message for easy identification in the Unity console.
	/// </remarks>
	internal static class ModLogger
	{
		/// <summary>
		///     The prefix used for each log message, based on the mod's short name.
		/// </summary>
		private const string Prefix = "[" + ModSettings.ShortName + "]";

		/// <summary>
		///     Logs an informational message to the Unity console.
		/// </summary>
		/// <param name="message">
		///     The message to log. Can be any object, and its <see cref="object.ToString" /> method will be
		///     called.
		/// </param>
		/// <remarks>
		///     This method wraps <see cref="Debug.Log" /> and adds the mod's prefix to the message.
		/// </remarks>
		internal static void Info(object message)
		{
			Debug.Log($"{Prefix} {message}");
		}

		/// <summary>
		///     Logs a warning message to the Unity console.
		/// </summary>
		/// <param name="message">
		///     The message to log as a warning. Can be any object, and its <see cref="object.ToString" /> method
		///     will be called.
		/// </param>
		/// <remarks>
		///     This method wraps <see cref="Debug.LogWarning" /> and adds the mod's prefix to the message.
		/// </remarks>
		internal static void Warn(object message)
		{
			Debug.LogWarning($"{Prefix} {message}");
		}

		/// <summary>
		///     Logs an error message to the Unity console.
		/// </summary>
		/// <param name="message">
		///     The message to log as an error. Can be any object, and its <see cref="object.ToString" /> method
		///     will be called.
		/// </param>
		/// <remarks>
		///     This method wraps <see cref="Debug.LogError" /> and adds the mod's prefix to the message.
		/// </remarks>
		internal static void Error(object message)
		{
			Debug.LogError($"{Prefix} {message}");
		}
	}
}