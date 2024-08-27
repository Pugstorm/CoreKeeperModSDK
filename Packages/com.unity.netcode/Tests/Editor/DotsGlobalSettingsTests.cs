using UnityEditor;
using NUnit.Framework;
using Unity.Entities.Build;
using Unity.NetCode.Tests;

namespace Unity.Scenes.Editor.Tests
{
    public class DotsGlobalSettingsTests : TestWithSceneAsset
    {
        [Test]
        public void NetCodeDebugDefine_IsSetForDevelopmentBuild()
        {
            var originalValue = EditorUserBuildSettings.development;
            try
            {
                EditorUserBuildSettings.development = true;
                var dotsSettings = DotsGlobalSettings.Instance;
                CollectionAssert.Contains(dotsSettings.ClientProvider.GetExtraScriptingDefines(), "NETCODE_DEBUG");
                CollectionAssert.Contains(dotsSettings.ServerProvider.GetExtraScriptingDefines(), "NETCODE_DEBUG");
                EditorUserBuildSettings.development = false;
                CollectionAssert.DoesNotContain(dotsSettings.ClientProvider.GetExtraScriptingDefines(),
                    "NETCODE_DEBUG");
                CollectionAssert.DoesNotContain(dotsSettings.ServerProvider.GetExtraScriptingDefines(),
                    "NETCODE_DEBUG");
            }
            finally
            {
                EditorUserBuildSettings.development = originalValue;
            }
        }
    }
}
