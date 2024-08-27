#if UNITY_EDITOR
using Unity.NetCode.Hybrid;

namespace Unity.NetCode.Tests
{
    /// <summary>
    /// Helper functions that helps building playmode tests.
    /// </summary>
    public static class PlaymodeUtils
    {
        /// <summary>
        /// Helper function to set the current build-target to client-only.
        /// Can be executed before a build by passing "-executeMethod Unity.NetCode.Tests.PlaymodeUtils.SetClientBuild" when launching the editor through command line.
        /// </summary>
        public static void SetClientBuild()
        {
            NetCodeClientSettings.instance.ClientTarget = NetCodeClientTarget.Client;
            NetCodeClientSettings.instance.Save();
        }
    }
}
#endif
