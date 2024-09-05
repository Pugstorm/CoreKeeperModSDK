using UnityEngine;

namespace CK_QOL_Collection.Core
{
    /// <summary>
    /// Provides logging functionality for the CK_QOL_Collection.
    /// This static class is used to log informational messages and errors to the Unity console.
    /// </summary>
    internal static class Logger
    {
        /// <summary>
        /// Logs an informational message to the Unity console.
        /// </summary>
        /// <param name="message">The message to be logged. Can be any object, which will be converted to a string.</param>
        /// <seealso cref="Debug.Log(object)"/>
        internal static void Info(object message)
        {
            Debug.Log($"[{Entry.Name}] {message}");
        }

        /// <summary>
        /// Logs an error message to the Unity console.
        /// </summary>
        /// <param name="message">The error message to be logged.</param>
        /// <seealso cref="Debug.LogError(object)"/>
        internal static void Error(string message)
        {
            Debug.LogError($"[{Entry.Name}] {message}");
        }
    }
}