using System;
using System.Collections.Generic;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode
{
    /// <summary>
    ///     Presets for the com.unity.transport simulator.
    ///     Allows developers to simulate a variety of network conditions.
    ///     <seealso cref="AppendBaseSimulatorPresets"/>
    ///     <seealso cref="AppendAdditionalMobileSimulatorProfiles"/>
    /// </summary>
    [Serializable]
    public struct SimulatorPreset
    {
        /// <summary>Users can modify simulator preset values directly. This preset is called "custom".</summary>
        internal const string k_CustomProfileKey = "Custom / User Defined";
        const string k_CustomProfileTooltip = "Custom indicates that you have modified individual simulator values yourself.";
        const string k_PoorMobileTooltip = "Extremely poor connection quality, completely unsuitable for synchronous multiplayer gaming due to exceptionally high latency. Turn based games <i>may</i> work.";
        const string k_DecentMobileTooltip = "Suitable for synchronous multiplayer, but expect connection instability.\n\nExpect to handle players dropping frequently, and dropping their connection entirely. I.e. Ensure you handle reconnections and quickly detect (and display) wifi issues.";
        const string k_MobileWifiDisclaimer = "Interestingly; while broadband is typical for desktop and console platforms, it's <i>also</i> the most common connection used by mobile players.";
        const string k_FiveGDisclaimer = "\n\n<i><b>In many places, expect this to be 'as good as' or 'better than' home broadband.</b></i>";
        const string k_MinSpecDisclaimer = "\n\n<i><b>This is the minimum supported mobile connection for synchronous gameplay. Expect high ping, jitter, stuttering and packet loss.</b></i>";
        const string k_PlayersAsGoodOrBetter = " players will have a connection as good as this or better.";
        const string k_Regional = "connection to a region-specific server (i.e. a server deployed to serve only their region, continent, or locale).";
        const string k_Perfect = "Represents a player on a \"perfect\" " + k_Regional + "\n\nI.e. Only 5% of " + k_PlayersAsGoodOrBetter;
        const string k_Decent = "Represents a player on a \"decent\" " + k_Regional + "\n\nI.e. Only 25% of " + k_PlayersAsGoodOrBetter;
        const string k_Average = "Represents a player on an \"average\" " + k_Regional + "\n\nI.e. Half of all " + k_PlayersAsGoodOrBetter;
        const string k_Poor = "Represents a player on a \"poor\" " + k_Regional + "\n\nWe strongly recommend testing with this connection quality to understand (and mitigate) how some of your users will experience the game.\n\nI.e. 95% of " + k_PlayersAsGoodOrBetter;
        const string k_InternationalDisclaimer = "\n\n\"International\": A game server deployed to a single region and served globally. Generally not suitable for synchronous multiplayer due to latency requirements, although this approach is appropriate for turn-based or asynchronous game servers.\n\n";
        const string k_InternationalDecent = "Represents a \"decent\" connection from a player connecting to a server hosted <b>outside their region</b>." + k_InternationalDisclaimer + "I.e. 25% of " + k_PlayersAsGoodOrBetter;
        const string k_InternationalAverage = "Represents an \"average\" connection from a player connecting to a server hosted <b>outside their region</b>." + k_InternationalDisclaimer + "I.e. Half of all " + k_PlayersAsGoodOrBetter;
        const string k_InternationalPoor = "Represents a \"poor\" connection from a player connecting to a server hosted <b>outside their region</b>." + k_InternationalDisclaimer + "I.e. 95% of " + k_PlayersAsGoodOrBetter;

        /// <summary>
        ///     The most common profiles, including custom debug ones.
        ///     Last updated Q3 2022.
        /// </summary>
        /// <param name="list">To append to.</param>
        public static void AppendBaseSimulatorPresets(List<SimulatorPreset> list)
        {
            list.Add(new SimulatorPreset(k_CustomProfileKey, -1, -1, -1, 0, k_CustomProfileTooltip));
            list.Add(new SimulatorPreset("Custom / No Internet", 1000, 1000, 100, 0,"Simulate the server becoming completely unreachable."));
            list.Add(new SimulatorPreset("Custom / Unplayable Internet", 300, 400, 30, 0, "Simulate barely having a connection at all, to observe what your users will experience when the internet is good enough to connect (sometimes), but not good enough to play.\n\nIt may take multiple attempts for the driver to connect.\n\nWe recommend detecting a \"minimum threshold of playable\", and to exclude (and inform) users when below this threshold."));
            list.Add(new SimulatorPreset("Custom / MitM (Man-in-the-Middle) Packet Corruption", 200, 400, 2, 1, "Simulate a malicious user attempting to catastrophically err your client, or (more likely) the server."));

            BuildProfiles(list, true, "Broadband [WIFI] / ", 1, 1, 1, k_MobileWifiDisclaimer);
        }

        /// <summary>
        ///     <para>These are best-estimate approximations for mobile connection types, informed by real world data.
        ///     Last updated Q3 2022.</para>
        ///     <para>Sources:</para>
        ///     <para>- Developers [Multiplayer, Support and Customers]</para>
        ///     <para>- https://unity.com/products/multiplay</para>
        ///     <para>- https://www.giffgaff.com/blog/h-5g-lte-a-g-e-new-cell-network-alphabet/</para>
        ///     <para>- https://www.4g.co.uk/how-fast-is-4g/</para>
        /// </summary>
        /// <param name="list">To append to.</param>
        public static void AppendAdditionalMobileSimulatorProfiles(List<SimulatorPreset> list)
        {
            BuildProfiles(list, false, "2G [!] [CDMA & GSM, '00] / ", 200, 20, 5, k_PoorMobileTooltip);
            BuildProfiles(list, false, "2.5G [!] [GPRS, G, '00] / ", 180, 15, 5, k_PoorMobileTooltip);
            BuildProfiles(list, false, "2.75G [!] [Edge, E, '06] / ", 160, 15, 5, k_PoorMobileTooltip);
            BuildProfiles(list, false, "3G [!] [WCDMA & UMTS, '03 ] / ", 120, 10, 5, k_PoorMobileTooltip);
            BuildProfiles(list, true, "3.5G [HSDPA, H, '06] / ", 65, 10, 5, k_DecentMobileTooltip + k_MinSpecDisclaimer);
            BuildProfiles(list, true, "3.75G [HDSDPA+, H+, '11] / ", 50, 10, 5, k_DecentMobileTooltip);
            BuildProfiles(list, true, "4G [4G, LTE, '13] / ", 35, 5, 3, k_DecentMobileTooltip);
            BuildProfiles(list, true, "4.5G [4G+, LTE-A, '16] / ", 25, 5, 3, k_DecentMobileTooltip);
            BuildProfiles(list, true, "5G ['20] / ", 0, 5, 3, k_DecentMobileTooltip + k_FiveGDisclaimer);
        }

        /// <summary>
        ///     <para>These are best-estimate approximations of PC and Console connection types, informed by real world data.
        ///     Last updated Q3 2022.</para>
        ///     <para>Sources:</para>
        ///     <para>- Developers [Multiplayer, Support and Customers]</para>
        ///     <para>- https://unity.com/products/multiplay</para>
        /// </summary>
        /// <param name="list">To append to.</param>
        public static void AppendAdditionalPCSimulatorPresets(List<SimulatorPreset> list)
        {
            list.Add(new SimulatorPreset("LAN [Local Area Network]", 1, 1, 1, 0, "Playing on LAN is generally <1ms (i.e. simulator off), but we've included it for convenience."));
        }

        /// <summary>Builds sub-profiles for your profile. E.g. 4 regional options for your custom profile.</summary>
        /// <param name="list">To append to.</param>
        /// <param name="showRegional">False for any profiles that are such poor quality, that you don't even want to allow users to select regional servers (as it would be pointless, and give the wrong impression).</param>
        /// <param name="name">Name of profile. Include a forward slash if you want sub-profiles to be in a sub-menu.</param>
        /// <param name="packetDelayMs">Note that profiles add delay on top.</param>
        /// <param name="packetJitterMs">Note that profiles add delay on top.</param>
        /// <param name="packetLossPercent">Note that profiles add delay on top.</param>
        /// <param name="tooltip">Note that profiles add delay on top.</param>
        public static void BuildProfiles(List<SimulatorPreset> list, bool showRegional, string name, int packetDelayMs, int packetJitterMs, int packetLossPercent, string tooltip)
        {
            if (tooltip != null)
                tooltip += "\n\n";

            if (showRegional)
            {
                list.Add(new SimulatorPreset(name + "Regional [5th Percentile]", packetDelayMs + 9, packetJitterMs + 1, packetLossPercent + 1, 0, tooltip + k_Perfect));
                list.Add(new SimulatorPreset(name + "Regional [25th Percentile]", packetDelayMs + 15, packetJitterMs + 5, packetLossPercent + 1, 0, tooltip + k_Decent));
                list.Add(new SimulatorPreset(name + "Regional [50th Percentile]", packetDelayMs + 65, packetJitterMs + 10, packetLossPercent + 2, 0, tooltip + k_Average));
                list.Add(new SimulatorPreset(name + "Regional [95th Percentile]", packetDelayMs + 150, packetJitterMs + 10, packetLossPercent + 3, 0, tooltip + k_Poor));
            }

            list.Add(new SimulatorPreset(name + "International [25th Percentile]", packetDelayMs + 60, packetJitterMs + 5, packetLossPercent + 2, 0, tooltip + k_InternationalDecent));
            list.Add(new SimulatorPreset(name + "International [50th Percentile]", packetDelayMs + 120, packetJitterMs + 10, packetLossPercent + 2, 0, tooltip + k_InternationalAverage));
            list.Add(new SimulatorPreset(name + "International [95th Percentile]", packetDelayMs + 200, packetJitterMs + 15, packetLossPercent + 5, 0, tooltip + k_InternationalPoor));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Returns appropriate presets for the targeted version.
        /// </summary>
        /// <param name="presetGroupName"></param>
        /// <param name="appendPresets"></param>
        public static void DefaultInUseSimulatorPresets(out string presetGroupName, List<SimulatorPreset> appendPresets)
        {
            appendPresets.Add(new SimulatorPreset(k_CustomProfileKey, -1, -1, -1, 0, k_CustomProfileTooltip));
            if (MultiplayerPlayModePreferences.ShowAllSimulatorPresets)
            {
                presetGroupName = "All Presets";
                AppendBaseSimulatorPresets(appendPresets);
                AppendAdditionalPCSimulatorPresets(appendPresets);
                AppendAdditionalMobileSimulatorProfiles(appendPresets);
            }
            else
            {
#if UNITY_IOS || UNITY_ANDROID
                presetGroupName = "Mobile Presets";
                AppendBaseSimulatorPresets(appendPresets);
                AppendAdditionalMobileSimulatorProfiles(appendPresets);
#else
                presetGroupName = "PC & Console Presets";
                AppendBaseSimulatorPresets(appendPresets);
                AppendAdditionalPCSimulatorPresets(appendPresets);
#endif
            }
        }
#endif

        /// <summary>
        /// True if this is user-defined the preset.
        /// </summary>
        public bool IsCustom => string.IsNullOrWhiteSpace(Name) || Name == k_CustomProfileKey;

        /// <summary>
        /// The name of the preset. Can be empty for custom preset (when the user modify the simulator setting in the editor).
        /// </summary>
        readonly internal string Name;
        /// <summary>
        /// The tooltip displayed in thet simulator window.
        /// </summary>
        readonly internal string Tooltip;
        /// <inheritdoc cref="Unity.Networking.Transport.Utilities.SimulatorUtility.Parameters.PacketDelayMs"/>
        internal int PacketDelayMs;
        /// <inheritdoc cref="Unity.Networking.Transport.Utilities.SimulatorUtility.Parameters.PacketJitterMs"/>
        internal int PacketJitterMs;
        /// <inheritdoc cref="Unity.Networking.Transport.Utilities.SimulatorUtility.Parameters.PacketDropPercentage"/>
        internal int PacketLossPercent;
        /// <inheritdoc cref="SimulatorUtility.Parameters.FuzzFactor"/>
        internal int PacketFuzzPercent;

        // TODO - Make use of bandwidth data in later commit.

        /// <summary>
        /// Retrieve the preset with the given <paramref name="name"/> from the <paramref name="allProfiles"/> list.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="allProfiles"></param>
        /// <param name="preset">the preset matching the name. null if the preset is not found.</param>
        /// <param name="index">the index of the preset in the list. -1 if the preset is not found.</param>
        /// <returns>true if the preset has been found</returns>
        internal static bool TryGetPresetFromName(string name, List<SimulatorPreset> allProfiles, out SimulatorPreset preset, out int index)
        {
            for (var i = 0; i < allProfiles.Count; i++)
            {
                preset = allProfiles[i];
                if (preset.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            preset = default;
            return false;
        }

        /// <summary>
        /// Construct a new preset.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="packetDelayMs"></param>
        /// <param name="packetJitterMs"></param>
        /// <param name="packetLossPercent"></param>
        /// <param name="packetFuzzPercent"></param>
        /// <param name="tooltip"></param>
        public SimulatorPreset(string name, int packetDelayMs, int packetJitterMs, int packetLossPercent, int packetFuzzPercent, string tooltip)
        {
            Name = name;
            Tooltip = tooltip;
            PacketDelayMs = packetDelayMs;
            PacketJitterMs = packetJitterMs;
            PacketLossPercent = packetLossPercent;
            PacketFuzzPercent = packetFuzzPercent;
        }

        /// <summary>
        /// Construct a new preset.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="packetDelayMs"></param>
        /// <param name="packetJitterMs"></param>
        /// <param name="packetLossPercent"></param>
        /// <param name="tooltip"></param>
        [Obsolete("Use other constructor. (RemovedAfter Entities 1.1)")]
        public SimulatorPreset(string name, int packetDelayMs, int packetJitterMs, int packetLossPercent, string tooltip)
            : this(name, packetDelayMs, packetJitterMs, packetLossPercent, 0, tooltip)
        {
        }
    }
}
