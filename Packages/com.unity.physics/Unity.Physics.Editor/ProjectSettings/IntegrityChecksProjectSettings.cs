using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class Preferences
{
    const string k_DisableIntegrityDefine = "UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS";
    const char k_defineSeparator = ';';

    public static bool IntegrityChecksDisabled
    {
        get => DefineExists(k_DisableIntegrityDefine);
        set => UpdateDefine(k_DisableIntegrityDefine, value);
    }

    [SettingsProvider]
    private static SettingsProvider IntegrityChecksMenuItem()
    {
        var provider = new SettingsProvider("Project/Physics/Unity Physics", SettingsScope.Project)
        {
            label = "Unity Physics",
            keywords = new[] { "Unity Physics", "Physics", "Enable Integrity Checks", "Disable Integrity Checks" },
            guiHandler = (searchContext) =>
            {
                bool oldIntegrityChecks = IntegrityChecksDisabled;
                bool newIntegrityChecks = EditorGUILayout.Toggle(new GUIContent("Enable Integrity Checks",
                    "Integrity checks should be disabled when measuring performance. Integrity checks should be enabled when checking simulation quality and behaviour."),
                    IntegrityChecksDisabled);
                if (newIntegrityChecks != oldIntegrityChecks)
                {
                    IntegrityChecksDisabled = newIntegrityChecks;
                    UpdateDefine(k_DisableIntegrityDefine, IntegrityChecksDisabled);
                }
            }
        };

        return provider;
    }

    private static void UpdateDefine(string define, bool add)
    {
        //collect all relevant build targets
        var buildTargetGroups = new List<BuildTargetGroup>();

        var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
        var activeBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);

        // Provide the define for activeBuildTargetGroup(e.g. Android, PS4)
        buildTargetGroups.Add(activeBuildTargetGroup);

        // Windows, Mac, Linux - always include these, as they are the only ones where the development happens
        // and could possibly want/not want integrity checks in the editor, as opposed to only the connected device.
        if (activeBuildTargetGroup != BuildTargetGroup.Standalone)
        {
            buildTargetGroups.Add(BuildTargetGroup.Standalone);
        }

        foreach (var buildTargetGroup in buildTargetGroups)
        {
            var fromBuildTargetGroup = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(fromBuildTargetGroup);

            // We add the separator at the end so we can add a new one if needed
            // Unity will automatically remove any unneeded separators at the end
            if (defines.Length > 0 && !defines.EndsWith("" + k_defineSeparator))
                defines += k_defineSeparator;

            var definesSb = new StringBuilder(defines);

            if (add)
            {
                // add at the end if it isn't already defined
                if (!defines.Contains(define))
                {
                    definesSb.Append(define);
                    definesSb.Append(k_defineSeparator);
                }
            }
            else
            {
                // find it and just replace that spot with and empty string
                var replaceToken = define + k_defineSeparator;
                definesSb.Replace(replaceToken, "");
            }

            PlayerSettings.SetScriptingDefineSymbols(fromBuildTargetGroup, definesSb.ToString());
        }
    }

    private static bool DefineExists(string define)
    {
        var fromBuildTargetGroup = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);
        var defines = PlayerSettings.GetScriptingDefineSymbols(fromBuildTargetGroup);
        return defines.Contains(define);
    }
}
