using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Hybrid
{
    /// <summary>
    /// Interface of the build settings that are used to build the client and server targets.
    /// </summary>
    internal interface INetCodeConversionTarget
    {
        NetcodeConversionTarget NetcodeTarget { get; }
    }

    /// <summary>
    /// A collection of extension utility methods for the <see cref="Baker{TAuthoringType}"/> used by NetCode during the baking process.
    /// </summary>
    public static class BakerExtensions
    {
        /// <summary>
        /// The current conversion target to use for the baking.
        /// </summary>
        /// <param name="self">an instance of the baker</param>
        /// <param name="isPrefab">state is we are converting a prefab or not</param>
        /// <typeparam name="T"></typeparam>
        /// <remarks>In the editor, if a <see cref="NetCodeConversionSettings"/> is present in the build configuration used for conversion,
        /// the target specified by the build component is used.
        /// <para>
        /// Otherwise, the conversion target will be determined by the destination world for runtime conversion, and fallback to always be
        /// <see cref="NetcodeConversionTarget.ClientAndServer"/> is nothing apply or for prefabs.
        /// </para>
        /// </remarks>
        /// <returns></returns>
        public static NetcodeConversionTarget GetNetcodeTarget<T>(this Baker<T> self, bool isPrefab) where T : Component
        {
            // Detect target using build settings (This is used from sub scenes)
#if UNITY_EDITOR
#if USING_PLATFORMS_PACKAGE
            if (self.TryGetBuildConfigurationComponent<NetCodeConversionSettings>(out var settings))
            {
                //Debug.LogWarning("BuildSettings conversion for: " + settings.Target);
                return settings.Target;
            }
#endif

            var settingAsset = self.GetDotsSettings();
            if (settingAsset is INetCodeConversionTarget asset)
            {
                return asset.NetcodeTarget;
            }
#endif

            // Prefabs are always converted as client and server when using convert to entity since they need to have a single blob asset
            if (!isPrefab)
            {
                if (self.IsClient())
                    return NetcodeConversionTarget.Client;
                if (self.IsServer())
                    return NetcodeConversionTarget.Server;
            }

            return NetcodeConversionTarget.ClientAndServer;
        }
    }
}
