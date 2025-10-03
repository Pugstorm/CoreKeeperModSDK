using System;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    class ProfilesEditor
    {
        public static string ValueGUILayout(AddressableAssetSettings settings, string label, string currentId)
        {
            string result = currentId;
            if (settings == null)
                return result;

            var displayNames = settings.profileSettings.GetVariableNames();
            AddressableAssetProfileSettings.ProfileIdData data = settings.profileSettings.GetProfileDataById(currentId);
            bool custom = data == null;

            int currentIndex = displayNames.Count;
            string toolTip = string.Empty;
            if (!custom)
            {
                currentIndex = displayNames.IndexOf(data.ProfileName);
                toolTip = Evaluate(settings, data);
            }

            displayNames.Add(AddressableAssetProfileSettings.customEntryString);


            var content = new GUIContent(label, toolTip);
            EditorGUILayout.BeginHorizontal();
            var newIndex = EditorGUILayout.Popup(content, currentIndex, displayNames.ToArray());
            if (newIndex != currentIndex)
            {
                if (displayNames[newIndex] == AddressableAssetProfileSettings.customEntryString)
                {
                    custom = true;
                    result = AddressableAssetProfileSettings.undefinedEntryValue;
                }
                else
                {
                    data = settings.profileSettings.GetProfileDataByName(displayNames[newIndex]);
                    if (data != null)
                        result = data.Id;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel += 1;
            if (custom)
                result = EditorGUILayout.TextField(result);
            else if (!string.IsNullOrEmpty(toolTip))
                EditorGUILayout.HelpBox(toolTip, MessageType.None);
            EditorGUI.indentLevel -= 1;
            return result;
        }

        public static float CalcGUIHeight(AddressableAssetSettings settings, string label, string currentId)
        {
            var labelContent = new GUIContent(label);
            var size = EditorStyles.popup.CalcSize(labelContent);
            var height = size.y + EditorGUIUtility.standardVerticalSpacing;
            AddressableAssetProfileSettings.ProfileIdData data = settings.profileSettings.GetProfileDataById(currentId);
            if (data != null)
            {
                var val = Evaluate(settings, data);
                var h = EditorStyles.helpBox.CalcHeight(new GUIContent(val), EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 16);
                return height + h;
            }

            return height + EditorStyles.textField.CalcHeight(new GUIContent(currentId), EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 16);
        }

        public static string ValueGUI(Rect rect, AddressableAssetSettings settings, string label, string currentId, ref bool wasChanged)
        {
            string result = currentId;
            if (settings == null)
                return result;

            var displayNames = settings.profileSettings.GetVariableNames();
            AddressableAssetProfileSettings.ProfileIdData data = settings.profileSettings.GetProfileDataById(currentId);
            bool custom = data == null && currentId != "—";

            int currentIndex = displayNames.Count;
            string toolTip = string.Empty;

            if (currentId == "—")
            {
                currentIndex = -1;
                toolTip = "—";
                result = "—";
            }
            else
            {
                if (!custom)
                {
                    currentIndex = displayNames.IndexOf(data.ProfileName);
                    toolTip = Evaluate(settings, data);
                }
            }

            displayNames.Add(AddressableAssetProfileSettings.customEntryString);

            var labelContent = new GUIContent(label);
            var size = EditorStyles.popup.CalcSize(labelContent);
            var topRect = new Rect(rect.x, rect.y, rect.width, size.y);

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUI.Popup(topRect, label, currentIndex, displayNames.ToArray());
            wasChanged = EditorGUI.EndChangeCheck();

            if (newIndex != -1 && newIndex != currentIndex)
            {
                if (displayNames[newIndex] == AddressableAssetProfileSettings.customEntryString)
                {
                    custom = true;
                    result = AddressableAssetProfileSettings.undefinedEntryValue;
                }
                else
                {
                    data = settings.profileSettings.GetProfileDataByName(displayNames[newIndex]);
                    if (data != null)
                        result = data.Id;
                }
            }

            var bottomRect = new Rect(rect.x, rect.y + size.y + EditorGUIUtility.standardVerticalSpacing, rect.width, rect.height - (size.y + EditorGUIUtility.standardVerticalSpacing));

            if (custom)
                result = EditorGUI.TextField(bottomRect, " ", result);
            else if (!string.IsNullOrEmpty(toolTip))
            {
                GUIStyle pathBox = new GUIStyle(EditorStyles.miniLabel);
                pathBox.wordWrap = true;
                EditorGUI.LabelField(bottomRect, " ", toolTip, pathBox);
            }

            return result;
        }

        static string Evaluate(AddressableAssetSettings settings, AddressableAssetProfileSettings.ProfileIdData data)
        {
            if (data.InlineUsage)
                return settings.profileSettings.EvaluateString(settings.activeProfileId, data.Id);

            string baseValue = settings.profileSettings.GetValueById(settings.activeProfileId, data.Id);
            return settings.profileSettings.EvaluateString(settings.activeProfileId, baseValue);
        }
    }
}
