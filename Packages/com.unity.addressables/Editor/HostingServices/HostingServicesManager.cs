using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Serialization;

// ReSharper disable DelegateSubtraction

namespace UnityEditor.AddressableAssets.HostingServices
{
    /// <summary>
    /// Manages the hosting services.
    /// </summary>
    [Serializable]
    public class HostingServicesManager : ISerializationCallbackReceiver
    {
        internal const string KPrivateIpAddressKey = "PrivateIpAddress";

        internal string GetPrivateIpAddressKey(int id = 0)
        {
            if (id == 0)
                return KPrivateIpAddressKey;
            return $"{KPrivateIpAddressKey}_{id}";
        }

        [Serializable]
        internal class HostingServiceInfo
        {
            [SerializeField]
            internal string classRef;

            [SerializeField]
            internal KeyDataStore dataStore;
        }

        [FormerlySerializedAs("m_hostingServiceInfos")]
        [SerializeField]
        List<HostingServiceInfo> m_HostingServiceInfos;

        [FormerlySerializedAs("m_settings")]
        [SerializeField]
        AddressableAssetSettings m_Settings;

        [FormerlySerializedAs("m_nextInstanceId")]
        [SerializeField]
        int m_NextInstanceId;

        [FormerlySerializedAs("m_registeredServiceTypeRefs")]
        [SerializeField]
        List<string> m_RegisteredServiceTypeRefs;

        readonly Type[] m_BuiltinServiceTypes =
        {
            typeof(HttpHostingService)
        };

        Dictionary<IHostingService, HostingServiceInfo> m_HostingServiceInfoMap;
        ILogger m_Logger;
        List<Type> m_RegisteredServiceTypes;

        [SerializeField]
        int m_PingTimeoutInMilliseconds = 5000;
        /// <summary>
        /// Timeout in milliseconds for filtering ip addresses for the hosting service
        /// </summary>
        internal int PingTimeoutInMilliseconds
        {
            get { return m_PingTimeoutInMilliseconds; }
            set { m_PingTimeoutInMilliseconds = value; }
        }

        /// <summary>
        /// Key/Value pairs valid for profile variable substitution
        /// </summary>
        public Dictionary<string, string> GlobalProfileVariables { get; private set; }

        internal static readonly string k_GlobalProfileVariablesCountKey = $"com.unity.addressables.{nameof(GlobalProfileVariables)}Count";

        internal static string GetSessionStateKey(int id)
        {
            return $"com.unity.addressables.{nameof(GlobalProfileVariables)}{id}";
        }

        /// <summary>
        /// Direct logging output of all managed services
        /// </summary>
        public ILogger Logger
        {
            get { return m_Logger; }
            set
            {
                m_Logger = value ?? Debug.unityLogger;
                foreach (var svc in HostingServices)
                    svc.Logger = m_Logger;
            }
        }

        /// <summary>
        /// Static method for use in starting up the HostingServicesManager in batch mode.
        /// </summary>
        /// <param name="settings"> </param>
        public static void BatchMode(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("Could not load Addressable Assets settings - aborting.");
                return;
            }

            var manager = settings.HostingServicesManager;
            if (manager == null)
            {
                Debug.LogError("Could not load HostingServicesManager - aborting.");
                return;
            }

            manager.StartAllServices();
        }

        /// <summary>
        /// Static method for use in starting up the HostingServicesManager in batch mode. This method
        /// without parameters will find and use the default <see cref="AddressableAssetSettings"/> object.
        /// </summary>
        public static void BatchMode()
        {
            BatchMode(AddressableAssetSettingsDefaultObject.Settings);
        }

        /// <summary>
        /// Indicates whether or not this HostingServiceManager is initialized
        /// </summary>
        public bool IsInitialized
        {
            get { return m_Settings != null; }
        }

        /// <summary>
        /// Return an enumerable list of all configured <see cref="IHostingService"/> objects
        /// </summary>
        public ICollection<IHostingService> HostingServices
        {
            get { return m_HostingServiceInfoMap.Keys; }
        }

        /// <summary>
        /// Get an array of all <see cref="IHostingService"/> types that have been used by the manager, or are known
        /// built-in types available for use.
        /// </summary>
        /// <returns></returns>
        public Type[] RegisteredServiceTypes
        {
            get
            {
                if (m_RegisteredServiceTypes.Count == 0)
                    m_RegisteredServiceTypes.AddRange(m_BuiltinServiceTypes);

                return m_RegisteredServiceTypes.ToArray();
            }
        }

        /// <summary>
        /// The id value that will be assigned to the next <see cref="IHostingService"/> add to the manager.
        /// </summary>
        public int NextInstanceId
        {
            get { return m_NextInstanceId; }
        }

        /// <summary>
        /// Create a new <see cref="HostingServicesManager"/>
        /// </summary>
        public HostingServicesManager()
        {
            GlobalProfileVariables = new Dictionary<string, string>();
            m_HostingServiceInfos = new List<HostingServiceInfo>();
            m_HostingServiceInfoMap = new Dictionary<IHostingService, HostingServiceInfo>();
            m_RegisteredServiceTypes = new List<Type>();
            m_RegisteredServiceTypeRefs = new List<string>();
            m_Logger = Debug.unityLogger;
        }

        /// <summary>
        /// Initialize manager with the given <see cref="AddressableAssetSettings"/> object.
        /// </summary>
        /// <param name="settings"></param>
        public void Initialize(AddressableAssetSettings settings)
        {
            if (IsInitialized) return;
            m_Settings = settings;
            RefreshGlobalProfileVariables();
        }

        /// <summary>
        /// Calls <see cref="IHostingService.StopHostingService"/> on all managed <see cref="IHostingService"/> instances
        /// where <see cref="IHostingService.IsHostingServiceRunning"/> is true
        /// </summary>
        public void StopAllServices()
        {
            foreach (var svc in HostingServices)
            {
                try
                {
                    if (svc.IsHostingServiceRunning)
                        svc.StopHostingService();
                }
                catch (Exception e)
                {
                    m_Logger.LogFormat(LogType.Error, e.Message);
                }
            }
        }

        /// <summary>
        /// Calls <see cref="IHostingService.StartHostingService"/> on all managed <see cref="IHostingService"/> instances
        /// where <see cref="IHostingService.IsHostingServiceRunning"/> is false
        /// </summary>
        public void StartAllServices()
        {
            foreach (var svc in HostingServices)
            {
                try
                {
                    if (!svc.IsHostingServiceRunning)
                        svc.StartHostingService();
                }
                catch (Exception e)
                {
                    m_Logger.LogFormat(LogType.Error, e.Message);
                }
            }
        }

        /// <summary>
        /// Add a new hosting service instance of the given type. The <paramref name="serviceType"/> must implement the
        /// <see cref="IHostingService"/> interface, or an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="serviceType">A <see cref="Type"/> object for the service. Must implement <see cref="IHostingService"/></param>
        /// <param name="name">A descriptive name for the new service instance.</param>
        /// <returns></returns>
        public IHostingService AddHostingService(Type serviceType, string name)
        {
            var svc = Activator.CreateInstance(serviceType) as IHostingService;
            if (svc == null)
                throw new ArgumentException("Provided type does not implement IHostingService", "serviceType");

            if (!m_RegisteredServiceTypes.Contains(serviceType))
                m_RegisteredServiceTypes.Add(serviceType);

            var info = new HostingServiceInfo
            {
                classRef = TypeToClassRef(serviceType),
                dataStore = new KeyDataStore()
            };

            svc.Logger = m_Logger;
            svc.DescriptiveName = name;
            svc.InstanceId = m_NextInstanceId;
            svc.HostingServiceContentRoots.AddRange(GetAllContentRoots());
            m_Settings.profileSettings.RegisterProfileStringEvaluationFunc(svc.EvaluateProfileString);

            m_HostingServiceInfoMap.Add(svc, info);
            m_Settings.SetDirty(AddressableAssetSettings.ModificationEvent.HostingServicesManagerModified, this, true, true);
            AddressableAssetUtility.OpenAssetIfUsingVCIntegration(m_Settings);

            m_NextInstanceId++;
            return svc;
        }

        /// <summary>
        /// Stops the given <see cref="IHostingService"/>, unregisters callbacks, and removes it from management. This
        /// function does nothing if the service is not being managed by this <see cref="HostingServicesManager"/>
        /// </summary>
        /// <param name="svc"></param>
        public void RemoveHostingService(IHostingService svc)
        {
            if (!m_HostingServiceInfoMap.ContainsKey(svc))
                return;

            svc.StopHostingService();
            m_Settings.profileSettings.UnregisterProfileStringEvaluationFunc(svc.EvaluateProfileString);
            m_HostingServiceInfoMap.Remove(svc);
            m_Settings.SetDirty(AddressableAssetSettings.ModificationEvent.HostingServicesManagerModified, this, true, true);
            AddressableAssetUtility.OpenAssetIfUsingVCIntegration(m_Settings);
        }

        /// <summary>
        /// Should be called by parent <see cref="ScriptableObject"/> instance Awake method
        /// </summary>
        internal void OnAwake()
        {
            RefreshGlobalProfileVariables();
        }

        /// <summary>
        /// Should be called by parent <see cref="ScriptableObject"/> instance OnEnable method
        /// </summary>
        public void OnEnable()
        {
            Debug.Assert(IsInitialized);

            m_Settings.OnModification -= OnSettingsModification;
            m_Settings.OnModification += OnSettingsModification;
            m_Settings.profileSettings.RegisterProfileStringEvaluationFunc(EvaluateGlobalProfileVariableKey);

            // GetAllContentRoots can return unpredictable results when there are no hosting services
            if (HostingServices.Count > 0)
            {
                var contentRoots = GetAllContentRoots();
                foreach (var svc in HostingServices)
                {

                    svc.Logger = m_Logger;
                    m_Settings.profileSettings.RegisterProfileStringEvaluationFunc(svc.EvaluateProfileString);
                    var baseSvc = svc as BaseHostingService;
                    svc.HostingServiceContentRoots.Clear();
                    svc.HostingServiceContentRoots.AddRange(contentRoots);
                    baseSvc?.OnEnable();
                }
            }

            LoadSessionStateKeysIfExists();
        }

        /// <summary>
        /// Should be called by parent <see cref="ScriptableObject"/> instance OnDisable method
        /// </summary>
        public void OnDisable()
        {
            Debug.Assert(IsInitialized);

            // ReSharper disable once DelegateSubtraction
            m_Settings.OnModification -= OnSettingsModification;
            m_Settings.profileSettings.UnregisterProfileStringEvaluationFunc(EvaluateGlobalProfileVariableKey);
            foreach (var svc in HostingServices)
            {
                svc.Logger = null;
                m_Settings.profileSettings.UnregisterProfileStringEvaluationFunc(svc.EvaluateProfileString);
                (svc as BaseHostingService)?.OnDisable();
            }

            SaveSessionStateKeys();
        }

        internal void LoadSessionStateKeysIfExists()
        {
            int numKeys = SessionState.GetInt(k_GlobalProfileVariablesCountKey, 0);
            if (numKeys > 0)
                GlobalProfileVariables.Clear();

            for (int i = 0; i < numKeys; i++)
            {
                string profileVar = SessionState.GetString(GetSessionStateKey(i), string.Empty);
                if (!string.IsNullOrEmpty(profileVar))
                    GlobalProfileVariables.Add(GetPrivateIpAddressKey(i), profileVar);
            }
        }

        internal void SaveSessionStateKeys()
        {
            int prevNumKeys = SessionState.GetInt(k_GlobalProfileVariablesCountKey, 0);
            SessionState.SetInt(k_GlobalProfileVariablesCountKey, GlobalProfileVariables.Count);

            int profileVarIdx = 0;
            foreach (KeyValuePair<string, string> pair in GlobalProfileVariables)
            {
                SessionState.SetString(GetSessionStateKey(profileVarIdx), pair.Value);
                profileVarIdx++;
            }

            EraseSessionStateKeys(profileVarIdx, prevNumKeys);
        }

        internal static void EraseSessionStateKeys()
        {
            int numKeys = SessionState.GetInt(k_GlobalProfileVariablesCountKey, 0);
            EraseSessionStateKeys(0, numKeys);
            SessionState.EraseInt(k_GlobalProfileVariablesCountKey);
        }

        static void EraseSessionStateKeys(int min, int max)
        {
            for (int i = min; i < max; i++)
            {
                SessionState.EraseString(GetSessionStateKey(i));
            }
        }

        /// <summary> Ensure object is ready for serialization, and calls <see cref="IHostingService.OnBeforeSerialize"/> methods
        /// on all managed <see cref="IHostingService"/> instances
        /// </summary>
        public void OnBeforeSerialize()
        {
            // https://docs.unity3d.com/ScriptReference/EditorWindow.OnInspectorUpdate.html
            // Because the manager is a serialized field in the Addressables settings, this method is called
            // at 10 frames per second when the settings are opened in the inspector...
            // Be careful what you put in there...

            m_HostingServiceInfos.Clear();
            foreach (var svc in HostingServices)
            {
                var info = m_HostingServiceInfoMap[svc];
                m_HostingServiceInfos.Add(info);
                svc.OnBeforeSerialize(info.dataStore);
            }

            m_RegisteredServiceTypeRefs.Clear();
            foreach (var type in m_RegisteredServiceTypes)
                m_RegisteredServiceTypeRefs.Add(TypeToClassRef(type));
        }

        /// <summary> Ensure object is ready for serialization, and calls <see cref="IHostingService.OnBeforeSerialize"/> methods
        /// on all managed <see cref="IHostingService"/> instances
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_HostingServiceInfoMap = new Dictionary<IHostingService, HostingServiceInfo>();
            foreach (var svcInfo in m_HostingServiceInfos)
            {
                IHostingService svc = CreateHostingServiceInstance(svcInfo.classRef);

                if (svc == null) continue;
                svc.OnAfterDeserialize(svcInfo.dataStore);
                m_HostingServiceInfoMap.Add(svc, svcInfo);
            }

            m_RegisteredServiceTypes = new List<Type>();
            foreach (var typeRef in m_RegisteredServiceTypeRefs)
            {
                var type = Type.GetType(typeRef, false);
                if (type == null) continue;
                m_RegisteredServiceTypes.Add(type);
            }
        }

        /// <summary>
        /// Refresh values in the global profile variables table.
        /// </summary>
        public void RefreshGlobalProfileVariables()
        {
            var vars = GlobalProfileVariables;
            vars.Clear();

            var ipAddressList = FilterValidIPAddresses(NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address).ToList());

            if (ipAddressList.Count > 0)
            {
                vars.Add(KPrivateIpAddressKey, ipAddressList[0].ToString());

                if (ipAddressList.Count > 1)
                {
                    for (var i = 1; i < ipAddressList.Count; i++)
                        vars.Add(KPrivateIpAddressKey + "_" + i, ipAddressList[i].ToString());
                }
            }
        }

        // Internal for unit tests
        internal string EvaluateGlobalProfileVariableKey(string key)
        {
            string retVal;
            GlobalProfileVariables.TryGetValue(key, out retVal);
            return retVal;
        }

        void OnSettingsModification(AddressableAssetSettings s, AddressableAssetSettings.ModificationEvent evt, object obj)
        {
            switch (evt)
            {
                case AddressableAssetSettings.ModificationEvent.GroupAdded:
                case AddressableAssetSettings.ModificationEvent.GroupRemoved:
                case AddressableAssetSettings.ModificationEvent.GroupSchemaAdded:
                case AddressableAssetSettings.ModificationEvent.GroupSchemaRemoved:
                case AddressableAssetSettings.ModificationEvent.GroupSchemaModified:
                case AddressableAssetSettings.ModificationEvent.ActiveProfileSet:
                case AddressableAssetSettings.ModificationEvent.BuildSettingsChanged:
                case AddressableAssetSettings.ModificationEvent.ProfileModified:
                    var profileRemoteBuildPath = m_Settings.profileSettings.GetValueByName(m_Settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath);
                    if (profileRemoteBuildPath != null && (profileRemoteBuildPath.Contains('[') || !CurrentContentRootsContain(profileRemoteBuildPath)))
                        ConfigureAllHostingServices();
                    break;
                case AddressableAssetSettings.ModificationEvent.BatchModification:
                    ConfigureAllHostingServices();
                    break;
            }
        }

        bool CurrentContentRootsContain(string root)
        {
            foreach (var svc in HostingServices)
            {
                if (!svc.HostingServiceContentRoots.Contains(root))
                    return false;
            }

            return true;
        }

        void ConfigureAllHostingServices()
        {
            if (HostingServices.Count > 0)
            {
                var contentRoots = GetAllContentRoots();

                foreach (var svc in HostingServices)
                {
                    svc.HostingServiceContentRoots.Clear();
                    svc.HostingServiceContentRoots.AddRange(contentRoots);
                }
            }
        }

        string[] GetAllContentRoots()
        {
            Debug.Assert(IsInitialized);

            var contentRoots = new List<string>();
            foreach (var group in m_Settings.groups)
            {
                if (group != null)
                {
                    foreach (var schema in group.Schemas)
                    {
                        var configProvider = schema as IHostingServiceConfigurationProvider;
                        if (configProvider != null)
                        {
                            var groupRoot = configProvider.HostingServicesContentRoot;
                            if (groupRoot != null && !contentRoots.Contains(groupRoot))
                                contentRoots.Add(groupRoot);
                        }
                    }
                }
            }

            return contentRoots.ToArray();
        }

        IHostingService CreateHostingServiceInstance(string classRef)
        {
            try
            {
                var objType = Type.GetType(classRef, true);
                var svc = (IHostingService)Activator.CreateInstance(objType);
                return svc;
            }
            catch (Exception e)
            {
                m_Logger.LogFormat(LogType.Error, "Could not create IHostingService from class ref '{0}'", classRef);
                m_Logger.LogFormat(LogType.Error, e.Message);
            }

            return null;
        }

        static string TypeToClassRef(Type t)
        {
            return string.Format("{0}, {1}", t.FullName, t.Assembly.GetName().Name);
        }

        // For unit tests
        internal AddressableAssetSettings Settings
        {
            get { return m_Settings; }
        }

        private List<IPAddress> FilterValidIPAddresses(List<IPAddress> ipAddresses)
        {
            List<IPAddress> validIpList = new List<IPAddress>();
            if (PingTimeoutInMilliseconds < 0)
            {
                m_Logger.LogFormat(LogType.Error, "Cannot filter IP addresses. Timeout must be a non-negative integer.");
                return validIpList;
            }

            foreach (IPAddress address in ipAddresses)
            {
                var sender = new System.Net.NetworkInformation.Ping();
                var reply = sender.Send(address.ToString(), PingTimeoutInMilliseconds);
                if (reply.Status == IPStatus.Success)
                {
                    validIpList.Add(address);
                }
            }

            return validIpList;
        }
    }
}
