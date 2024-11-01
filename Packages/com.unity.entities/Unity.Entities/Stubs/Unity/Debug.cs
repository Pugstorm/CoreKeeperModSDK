using System;
using UnityObject = UnityEngine.Object;

namespace Unity
{
    // TODO: provide an implementation of Unity.Debug that does not rely on UnityEngine and modernizes this API
    // (for now it's just here for easier compatibility and fwd migration)
    static class Debug
    {
        public static void LogError(object message) =>
            UnityEngine.Debug.LogError(message);
        public static void LogWarning(object message) =>
            UnityEngine.Debug.LogWarning(message);
        public static void Log(object message) =>
            UnityEngine.Debug.Log(message);
        public static void LogException(Exception exception) =>
            UnityEngine.Debug.LogException(exception);

        public static void LogError(object message, UnityObject context) =>
            UnityEngine.Debug.LogError(message, context);
        public static void LogWarning(object message, UnityObject context) =>
            UnityEngine.Debug.LogWarning(message, context);
        public static void Log(object message, UnityObject context) =>
            UnityEngine.Debug.Log(message, context);
        public static void LogException(Exception exception, UnityObject context) =>
            UnityEngine.Debug.LogException(exception, context);
    }
}
