#if UNITY_EDITOR || NETCODE_DEBUG
using System;
using System.IO;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode
{
    /// <summary>
    ///     In the Editor, <see cref="MultiplayerPlayModePreferences"/> are used.
    ///     In development builds, json params can be loaded and enabled via command line arg.
    ///     In prod builds, the network simulator is always disabled.
    /// </summary>
    public static class NetworkSimulatorSettings
    {
#if UNITY_EDITOR
        /// <summary>Are the UTP Network Simulator stages in use? In the editor, the value comes from the 'Multiplayer PlayTools Window'.</summary>
        public static bool Enabled => MultiplayerPlayModePreferences.SimulatorEnabled;
        /// <summary>Values to use in simulation. Set values via 'Multiplayer PlayTools Window'.</summary>
        public static SimulatorUtility.Parameters ClientSimulatorParameters => MultiplayerPlayModePreferences.ClientSimulatorParameters;
#else
        /// <summary>Are the UTP Network Simulator stages in use? Toggleable in development build.</summary>
        public static bool Enabled { get; private set; }
        /// <summary>Values to use in simulation. Set this to whatever you'd like in a development build.</summary>
        public static SimulatorUtility.Parameters ClientSimulatorParameters { get; private set; }
#endif

        static NetworkSimulatorSettings()
        {
#if !UNITY_EDITOR
            CheckCommandLineArgs();
#endif
        }

        /// <summary>A decent default for testing realistic, poor network conditions.</summary>
        public static SimulatorUtility.Parameters DefaultSimulatorParameters => new SimulatorUtility.Parameters
            {
                Mode = ApplyMode.AllPackets, MaxPacketSize = NetworkParameterConstants.MTU, MaxPacketCount = 200,
                FuzzFactor = 0, PacketDelayMs = 100, PacketJitterMs = 10, PacketDropPercentage = 1, PacketDuplicationPercentage = 1
            };

#if !UNITY_EDITOR
        /// <summary>
        ///     Checks for the existence of `--loadNetworkSimulatorJsonFile`, which, if set, will set <see cref="Enabled"/> to true, and write <see cref="ClientSimulatorParameters"/>.
        ///     If no file is found, logs an error, and defaults to <see cref="DefaultSimulatorParameters"/>. Use `--createNetworkSimulatorJsonFile` to automatically generate the file instead.
        /// </summary>
        public static void CheckCommandLineArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                var createTriggered = string.Compare(args[i], "--createNetworkSimulatorJsonFile", StringComparison.OrdinalIgnoreCase) == 0;
                var useTriggered = !createTriggered && string.Compare(args[i], "--loadNetworkSimulatorJsonFile", StringComparison.OrdinalIgnoreCase) == 0;
                if (createTriggered || useTriggered)
                {
                    var simulatorJsonFilePath = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.OrdinalIgnoreCase) ? args[i + 1] : "NetworkSimulatorProfile.json";
                    simulatorJsonFilePath = Path.GetFullPath(simulatorJsonFilePath);

                    if (!simulatorJsonFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        simulatorJsonFilePath += ".json";

                    var fileInfo = new FileInfo(simulatorJsonFilePath);
                    fileInfo.Refresh();
                    if (!fileInfo.Exists)
                    {
                        if (createTriggered)
                        {
                            UnityEngine.Debug.Log($"Commandline arg '--createNetworkSimulatorJsonFile' passed, but no JSON file found at path '{fileInfo.FullName}'. Creating a 'default' one now using `DefaultSimulatorParameters`.");
                            var json = UnityEngine.JsonUtility.ToJson(DefaultSimulatorParameters, true);
                            File.WriteAllText(fileInfo.FullName, json);
                        }
                        else
                        {
                            UnityEngine.Debug.LogError($"Commandline arg '--loadNetworkSimulatorJsonFile' passed, but no JSON file found at path '{fileInfo.FullName}'. Using `DefaultSimulatorParameters` instead.");
                            Enabled = true;
                            ClientSimulatorParameters = DefaultSimulatorParameters;
                        }
                    }

                    try
                    {
                        var jsonText = File.ReadAllText(fileInfo.FullName);
                        ClientSimulatorParameters = UnityEngine.JsonUtility.FromJson<SimulatorUtility.Parameters>(jsonText);
                        Enabled = true;
                        UnityEngine.Debug.Log($"Enabled network simulator via command line arg '--loadNetworkSimulatorJsonFile' using '{fileInfo.FullName}': {ClientSimulatorParameters.Mode} with {ClientSimulatorParameters.PacketDelayMs}±{ClientSimulatorParameters.PacketJitterMs}ms!");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Exception thrown attempting to enable network simulator via command line arg '--loadNetworkSimulatorJsonFile' while applying JSON file '{fileInfo.FullName}'. Exception: '{e}'!");
                    }
                    break;
                }
            }
        }
#endif

        /// <summary>
        ///     Utility to cycle through drivers and update their simulator pipelines with the inputted settings.
        /// </summary>
        /// <param name="parameters">Settings to apply to live drivers.</param>
        /// <param name="store">Store used to retrieve drivers from.</param>
        public static void RefreshSimulationPipelineParametersLive(in SimulatorUtility.Parameters parameters, ref NetworkDriverStore store)
        {
            for (var i = store.FirstDriver; i <= store.LastDriver; ++i)
            {
                var driverInstance = store.GetDriverInstance(i);
                if (!driverInstance.simulatorEnabled) continue;

                var driverCurrentSettings = driverInstance.driver.CurrentSettings;
                var simParams = driverCurrentSettings.GetSimulatorStageParameters();
                simParams.Mode = parameters.Mode;
                simParams.PacketDelayMs = parameters.PacketDelayMs;
                simParams.PacketJitterMs = parameters.PacketJitterMs;
                simParams.PacketDropPercentage = 0; // // Set this to zero to avoid applying packet loss twice.
                simParams.PacketDropInterval = parameters.PacketDropInterval;
                simParams.PacketDuplicationPercentage = parameters.PacketDuplicationPercentage;
                simParams.FuzzFactor = parameters.FuzzFactor;
                simParams.FuzzOffset = parameters.FuzzOffset;
                driverInstance.driver.ModifySimulatorStageParameters(simParams);

                // This new simulator has less features, but it does allow us to drop ALL packets (even low-level connection ones),
                // allowing us to test timeouts etc. Setting it instead of on the "light simulator".
                driverInstance.driver.ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter
                {
                    ReceivePacketLossPercent = parameters.PacketDropPercentage,
                    SendPacketLossPercent = parameters.PacketDropPercentage,
                });
            }
        }

        /// <summary>
        /// Convenience that handles the new nuance of `PacketDropPercentage` applying to two pipelines.
        /// </summary>
        /// <param name="settings">Settings to modify.</param>
        public static void SetSimulatorSettings(ref NetworkSettings settings)
        {
            var parameters = ClientSimulatorParameters;
            // This new simulator has less features, but it does allow us to drop ALL packets (even low-level connection ones),
            // allowing us to test timeouts etc. Setting it instead of on the "light simulator".
            settings.WithNetworkSimulatorParameters(parameters.PacketDropPercentage, parameters.PacketDropPercentage);

            // Thus, set this to zero to avoid applying packet loss twice.
            parameters.PacketDropPercentage = 0;
            settings.AddRawParameterStruct(ref parameters);
        }
    }
}
#endif
