using UnityEditor;

namespace Unity.Entities.Editor
{
    public static class ScriptTemplates
    {
        public const string ScriptTemplatePath = "Packages/com.unity.entities/Unity.Entities.Editor/ScriptTemplates/";

        [MenuItem("Assets/Create/Entities/IComponentData Script", priority = 1)]
        static void CreateIComponentDataScript()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}IComponentDataTemplate.txt", "NewIComponentDataScript.cs");
        }

        [MenuItem("Assets/Create/Entities/ISystem Script", priority = 2)]
        static void CreateISystemScript()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}ISystemTemplate.txt", "NewISystemScript.cs");
        }

        [MenuItem("Assets/Create/Entities/IJobEntity Script", priority = 3)]
        static void CreateIJobEntityScript()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}IJobEntityTemplate.txt", "NewIJobEntityScript.cs");
        }

        [MenuItem("Assets/Create/Entities/Baker Script", priority = 4)]
        static void CreateBakerScript()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}BakerTemplate.txt", "NewBakerScript.cs");
        }
    }
}
