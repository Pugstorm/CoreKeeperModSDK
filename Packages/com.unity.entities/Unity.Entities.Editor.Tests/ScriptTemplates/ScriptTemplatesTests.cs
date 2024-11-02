using UnityEditor;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Entities.Editor.Tests
{
    class ScriptTemplatesTests
    {   
        string[] paths = new string[]
        {
            $"{ScriptTemplates.ScriptTemplatePath}IComponentDataTemplate.txt",
            $"{ScriptTemplates.ScriptTemplatePath}ISystemTemplate.txt",
            $"{ScriptTemplates.ScriptTemplatePath}IJobEntityTemplate.txt",
            $"{ScriptTemplates.ScriptTemplatePath}BakerTemplate.txt",
        };
        
        [Test]
        public void ScriptTemplatesExist()
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);

                Assert.NotNull(asset);
            }
        }
    }
}
