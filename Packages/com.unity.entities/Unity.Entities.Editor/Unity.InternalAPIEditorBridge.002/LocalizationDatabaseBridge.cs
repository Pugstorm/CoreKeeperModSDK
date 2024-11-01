using UnityEditor;

namespace Unity.Editor.Bridge
{
    static class LocalizationDatabaseBridge
    {
        public static string GetLocalizedString(string original) => LocalizationDatabase.GetLocalizedString(original);
    }
}
