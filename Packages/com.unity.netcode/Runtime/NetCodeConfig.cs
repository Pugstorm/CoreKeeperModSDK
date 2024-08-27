using System;
using System.Text;
using UnityEngine;

namespace Unity.NetCode
{
    /// <summary>
    ///     Config file, allowing the package user to tweak netcode variables without having to write code.
    ///     Create as many instances as you like.
    /// </summary>
    [CreateAssetMenu(menuName = "NetCode/NetCodeConfig Asset", fileName = "NetCodeConfig")]
    public class NetCodeConfig : ScriptableObject, IComparable<NetCodeConfig>
    {
        /// <summary>
        ///     The Default NetcodeConfig asset, selected in ProjectSettings via the NetCode tab,
        ///     and fetched at runtime via the PreloadedAssets. Set via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>.
        /// </summary>
        public static NetCodeConfig Global { get; private set; }

        /// <summary> <see cref="ClientServerBootstrap"/> to either be <see cref="EnableAutomaticBootstrap"/> or <see cref="DisableAutomaticBootstrap"/>.</summary>
        public enum AutomaticBootstrapSetting
        {
            /// <summary>ENABLES the default <see cref="Unity.Entities.ICustomBootstrap"/> Entities bootstrap.</summary>
            EnableAutomaticBootstrap = 1,
            /// <summary>DISABLES the default <see cref="Unity.Entities.ICustomBootstrap"/> Entities bootstrap.</summary>
            /// <remarks>Only the Local world will be created, as if you called <see cref="ClientServerBootstrap.CreateLocalWorld"/>.</remarks>
            DisableAutomaticBootstrap = 0,
        }

        /// <summary>
        /// Netcode helper: Allows you to add multiple configs to the PreloadedAssets list. There can only be one global one.
        /// </summary>
        [HideInInspector]
        public bool IsGlobalConfig;

        /// <summary>
        ///     Denotes if the ClientServerBootstrap (or any derived version of it) should be triggered on game boot. Project-wide
        ///     setting, overridable via the OverrideAutomaticNetCodeBootstrap MonoBehaviour.
        /// </summary>
        [Header("NetCode")]
        [Tooltip("Denotes if the ClientServerBootstrap (or any derived version of it) should be triggered on game boot. Project-wide setting (when this config is applied in the Netcode tab), overridable via the OverrideAutomaticNetCodeBootstrap MonoBehaviour.")] [SerializeField]
        public AutomaticBootstrapSetting EnableClientServerBootstrap = AutomaticBootstrapSetting.EnableAutomaticBootstrap;

        // TODO - Range + Tooltips attributes for these structs.
        // TODO - Add a helper link to open the NetDbg when viewing the NetConfig asset.
        /// <inheritdoc cref="Unity.NetCode.ClientServerTickRate" path="/summary"/>
        public ClientServerTickRate ClientServerTickRate;
        /// <inheritdoc cref="Unity.NetCode.ClientTickRate"/>
        public ClientTickRate ClientTickRate;
        // TODO - World creation options.
        // TODO - Thin Client options.
        /// <inheritdoc cref="Unity.NetCode.GhostSendSystemData"/>
        public GhostSendSystemData GhostSendSystemData;
        // TODO - Importance.
        // TODO - Relevancy.

        //[Header("Unity Transport Package (UTP)")]
        // TODO - Make these structs public and [Serializable] so that we can actually modify them.
        // public NetworkConfigParameter NetworkConfigParameter;
        // public FragmentationUtility.Parameters FragmentationUtilityParameters;
        // public ReliableUtility.Parameters ReliableUtilityParameters;
        // public RelayNetworkParameter RelayNetworkParameter;

        internal NetCodeConfig()
        {
            // Note that these will be clobbered by any ScriptableObject in-place deserialization.
            Reset();
        }

        /// <summary>Setup default values.</summary>
        public void Reset()
        {
            ClientServerTickRate = default;
            ClientServerTickRate.ResolveDefaults();
            ClientTickRate = NetworkTimeSystem.DefaultClientTickRate;
            GhostSendSystemData = default;
            GhostSendSystemData.Initialize();
        }

        /// <summary>
        ///     Fetch the existing NetCodeConfig (from Resources), or, if not found, create one.
        /// </summary>
        /// <remarks><see cref="RuntimeInitializeLoadType.AfterAssembliesLoaded"/> guarantees that this is called BEFORE Entities initialization.</remarks>
        /// <returns></returns>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RuntimeTryFindSettings()
        {
            var configs = Resources.FindObjectsOfTypeAll<NetCodeConfig>();
            Array.Sort(configs);
            if (configs.Length > 0)
            {
                var hasError = false;
                var errSb = new StringBuilder($"[NetCodeConfig] Discovered {configs.Length} NetcodeConfig files in Resources. Using '{configs[0].name}', but the following errors occured:");
                for (var i = 0; i < configs.Length; i++)
                {
                    var config = configs[i];
                    errSb.Append($"\n[{i}] {config.name} (global: {config.IsGlobalConfig})");
                    if (i == 0)
                    {
                        if (!config.IsGlobalConfig)
                        {
                            hasError = true;
                            errSb.Append($"\t <-- Expected this to have IsGlobalConfig flag set!");
                        }
                    }
                    else
                    {
                        if (config.IsGlobalConfig)
                        {
                            hasError = true;
                            errSb.Append($"\t <-- Expected this NOT to have IsGlobalConfig set!");
                        }
                    }
                }

                if (hasError)
                {
                    errSb.Append("\nImplies an error during ProjectSettings selection!");
                    Debug.LogError(errSb);
                }
            }
            Global = configs.Length > 0 ? configs[0] : null;
        }

        /// <summary>
        ///     Makes Find deterministic.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(NetCodeConfig other)
        {
            if (IsGlobalConfig != other.IsGlobalConfig)
                return -IsGlobalConfig.CompareTo(other.IsGlobalConfig);
            return string.Compare(name, other.name, StringComparison.Ordinal);
        }
    }
}
