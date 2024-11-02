#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Properties;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.Entities.EntitiesJournaling;

namespace Unity.Entities.Editor
{
    partial class EntitiesJournalingWindow : DOTSEditorWindow
    {
        internal class ComponentDataValuesVisitor : IPropertyBagVisitor, IPropertyVisitor
        {
            readonly HashSet<string> m_Values = new HashSet<string>();

            public string[] GetValues()
            {
                var values = m_Values.ToArray();
                m_Values.Clear();
                return values;
            }

            void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
            {
                foreach (var property in properties.GetProperties(ref container))
                    property.Accept(this, ref container);
            }

            void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
            {
                var value = property.GetValue(ref container);

                // Special handling for some containers
                if (typeof(TValue) == typeof(Entity) ||
                    typeof(TValue) == typeof(EntityGuid) ||
                    typeof(TValue) == typeof(Hash128) ||
                    typeof(TValue) == typeof(FixedString32Bytes) ||
                    typeof(TValue) == typeof(FixedString64Bytes) ||
                    typeof(TValue) == typeof(FixedString128Bytes) ||
                    typeof(TValue) == typeof(FixedString512Bytes) ||
                    typeof(TValue) == typeof(FixedString4096Bytes))
                {
                    m_Values.Add(value.ToString());
                    return; // Do not visit property, stop here
                }

                // Add value as string if its not a container
                if (!TypeTraits<TValue>.IsContainer)
                    m_Values.Add(value.ToString());

                // Re-entry will invoke the strongly typed visit callback for this container
                PropertyContainer.Accept(this, value);
            }
        }

        internal const string k_RecordIndexToken = "ri";
        internal const string k_RecordTypeToken = "rt";
        internal const string k_FrameIndexToken = "f";
        internal const string k_WorldNameToken = "w";
        internal const string k_WorldIndexToken = "wi";
        internal const string k_SystemToken = "s";
        internal const string k_ExecutingSystemToken = "es";
        internal const string k_OriginSystemToken = "os";
        internal const string k_EntityNameToken = "e";
        internal const string k_EntityIndexToken = "ei";
        internal const string k_EntityCountToken = "ec";
        internal const string k_ComponentTypeNameToken = "c";
        internal const string k_ComponentTypeIndexToken = "ci";
        internal const string k_ComponentCountToken = "cc";
        internal const string k_ComponentDataValueToken = "v";

        const string k_RecordIndexColumn = "record-index";
        const string k_RecordTypeColumn = "record-type";
        const string k_SummaryColumn = "summary";
        const string k_FrameIndexColumn = "frame-index";
        const string k_WorldColumn = "world";
        const string k_ExecutingSystemColumn = "executing-system";
        const string k_OriginSystemColumn = "origin-system";
        const string k_EntitiesColumn = "entities";
        const string k_ComponentsColumn = "components";

        static readonly string s_WindowName = L10n.Tr("Journaling");
        static readonly string s_RecordTooltip = L10n.Tr("Toggle recording");
        static readonly string s_ExportTooltip = L10n.Tr("Export to CSV");
        static readonly string s_ClearTooltip = L10n.Tr("Clear recorded data");
        static readonly string s_DetailsTooltip = L10n.Tr("Toggle details view");
        static readonly string s_RecordIndex = L10n.Tr("Record Index");
        static readonly string s_RecordType = L10n.Tr("Record Type");
        static readonly string s_Summary = L10n.Tr("Summary");
        static readonly string s_FrameIndex = L10n.Tr("Frame");
        static readonly string s_World = L10n.Tr("World");
        static readonly string s_WorldName = L10n.Tr("World Name");
        static readonly string s_WorldIndex = L10n.Tr("World Sequence Number");
        static readonly string s_System = L10n.Tr("System");
        static readonly string s_ExecutingSystem = L10n.Tr("Executing System");
        static readonly string s_OriginSystem = L10n.Tr("Origin System");
        static readonly string s_EntityName = L10n.Tr("Entity Name");
        static readonly string s_EntityIndex = L10n.Tr("Entity Index");
        static readonly string s_Entities = L10n.Tr("Entities");
        static readonly string s_EntityCount = L10n.Tr("Entity Count");
        static readonly string s_Component = L10n.Tr("Component");
        static readonly string s_ComponentTypeName = L10n.Tr("Component Type Name");
        static readonly string s_ComponentTypeIndex = L10n.Tr("Component Type Index");
        static readonly string s_Components = L10n.Tr("Components");
        static readonly string s_ComponentCount = L10n.Tr("Component Count");
        static readonly string s_ComponentDataValue = L10n.Tr("Component Data Value");
        static readonly string s_RecordingMessage = L10n.Tr("Journaling data is currently being recorded. Stop recording to view recorded data.");
        static readonly string s_PostProcessingMessage = L10n.Tr("Post processing journaling data, this might take some time, please wait.");
        static readonly string s_Stop = L10n.Tr("Stop Recording");
        static readonly string s_SearchResult = L10n.Tr("{0} Results Found");
        static readonly string s_Records = L10n.Tr("{0} Records");
        static readonly string s_MemoryUsed = L10n.Tr("Memory Used: {0} / {1}");

        static readonly Lazy<Texture2D> s_JournalingIcon = new Lazy<Texture2D>(() => PackageResources.LoadIcon("Journaling/Journaling.png"));
        static readonly VisualElementTemplate s_WindowTemplate = PackageResources.LoadTemplate("Journaling/entities-journaling-window");
        static readonly VisualElementTemplate s_ContentTemplate = PackageResources.LoadTemplate("Journaling/entities-journaling-content");

        [MenuItem(Constants.MenuItems.JournalingWindow, false, Constants.MenuItems.JournalingWindowPriority)]
        static void OpenWindow() => GetWindow<EntitiesJournalingWindow>();

        internal static void RefreshWindow()
        {
            if (HasOpenInstances<EntitiesJournalingWindow>())
                GetWindow<EntitiesJournalingWindow>().Refresh();
        }

        WorldProxyManager m_WorldProxyManager;
        ComponentDataValuesVisitor m_ComponentDataValuesVisitor;
        ReadOnlyRecordViewList m_Records;
        SearchableList<MultiColumnListView, RecordView> m_RecordsList;
        int m_NeedPostProcess;

        Toggle m_RecordToggle;
        VisualElement m_MessageContainer;
        Label m_MessageLabel;
        Button m_StopButton;
        TwoPaneSplitView m_SplitPane;
        VisualElement m_ContentContainer;
        EntitiesJournalingWindowDetails m_Details;
        Label m_SearchResultLabel;
        Label m_RecordCountLabel;
        Label m_UsedBytesLabel;

        public EntitiesJournalingWindow() : base(Analytics.Window.Journaling)
        { }

        protected override void OnCreate()
        {
            m_WorldProxyManager = new WorldProxyManager();
            m_ComponentDataValuesVisitor = new ComponentDataValuesVisitor();
            m_NeedPostProcess = 0;

            titleContent = new GUIContent(s_WindowName, s_JournalingIcon.Value);
            minSize = Constants.MinWindowSize;

            var window = s_WindowTemplate.Clone();
            var toolbar = window.Q<Toolbar>("toolbar");
            var isRecording = Enabled;
            Preferences.Enabled = isRecording;

            m_RecordToggle = toolbar.Q<Toggle>("record");
            m_RecordToggle.value = Preferences.Enabled;
            m_RecordToggle.tooltip = s_RecordTooltip;
            m_RecordToggle.RegisterValueChangedCallback((e) =>
            {
                Enabled = e.newValue;
                Preferences.Enabled = e.newValue;
                Refresh();
            });

            var exportButton = toolbar.Q<Button>("export");
            exportButton.tooltip = s_ExportTooltip;
            exportButton.clicked += () =>
            {
                EntitiesJournalingUtilities.ExportToCSV();
            };

            var clearButton = toolbar.Q<Button>("clear");
            clearButton.text = "Clear";
            clearButton.tooltip = s_ClearTooltip;
            clearButton.clicked += () =>
            {
                Clear();
                m_Details.SetRecord(RecordView.Null);
                Refresh();
            };

            var searchContainer = window.Q("search-container");
            var searchElement = searchContainer.Q<SearchElement>("search");
            var defaultSearchOptions = new SearchFilterOptions();
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, ulong>(k_RecordIndexToken, r => r.Index, s_RecordIndex, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, string>(k_RecordTypeToken, r => r.RecordType.ToString(), s_RecordType);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, int>(k_FrameIndexToken, r => r.FrameIndex, s_FrameIndex, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, string>(k_WorldNameToken, GetWorldName, s_WorldName);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, ulong>(k_WorldIndexToken, r => r.World.SequenceNumber, s_WorldIndex, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, IEnumerable<string>>(k_SystemToken, r => new[] { GetExecutingSystemName(r), GetOriginSystemName(r) }, s_System);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, string>(k_ExecutingSystemToken, GetExecutingSystemName, s_ExecutingSystem);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, string>(k_OriginSystemToken, GetOriginSystemName, s_OriginSystem);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, IEnumerable<string>>(k_EntityNameToken, r => r.Entities.Select(e => e.Name), s_EntityName);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, IEnumerable<int>>(k_EntityIndexToken, r => r.Entities.Select(e => e.Index), s_EntityIndex, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, int>(k_EntityCountToken, r => r.Entities.Length, s_EntityCount, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, IEnumerable<string>>(k_ComponentTypeNameToken, r => r.ComponentTypes.Select(t => t.Name), s_ComponentTypeName);
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, IEnumerable<int>>(k_ComponentTypeIndexToken, r => r.ComponentTypes.Select(t => t.TypeIndex.Value), s_ComponentTypeIndex, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, int>(k_ComponentCountToken, r => r.ComponentTypes.Length, s_ComponentCount, "", defaultSearchOptions, "=");
            searchElement.AddSearchFilterCallbackWithPopupItem<RecordView, IEnumerable<string>>(k_ComponentDataValueToken, GetComponentDataText, s_ComponentDataValue);

            var jump = toolbar.Q<Button>("jump");
            SearchUtils.SetupJumpButton(jump, () => JournalSearchProvider.OpenProvider(searchElement.value));

            var detailsButton = toolbar.Q<Button>("details");
            detailsButton.tooltip = s_DetailsTooltip;
            detailsButton.clicked += () =>
            {
                m_Details.ToggleVisibility();
            };

            m_MessageContainer = window.Q("message-container");
            m_MessageContainer.SetVisibility(isRecording);
            m_MessageLabel = m_MessageContainer.Q<Label>("message");
            m_MessageLabel.text = isRecording ? s_RecordingMessage : m_NeedPostProcess > 0 ? s_PostProcessingMessage : string.Empty;

            m_StopButton = window.Q<Button>("stop");
            m_StopButton.text = s_Stop;
            m_StopButton.clicked += () =>
            {
                Enabled = false;
                Preferences.Enabled = false;
                m_RecordToggle.SetValueWithoutNotify(false);
                if (EditorApplication.isPlaying)
                    EditorApplication.isPaused = true;
                Refresh();
            };
            m_StopButton.SetVisibility(isRecording);

            m_ContentContainer = s_ContentTemplate.Clone(window.Q("content"));
            m_ContentContainer.SetVisibility(!isRecording && m_NeedPostProcess == 0);

            m_SplitPane = m_ContentContainer.Q<TwoPaneSplitView>("split-pane");
            m_SplitPane.RegisterCallback<GeometryChangedEvent>(OnInitialTwoPaneSplitViewGeometryChangedEvent);

            var recordsList = m_ContentContainer.Q<MultiColumnListView>("records");
            recordsList.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            recordsList.columns[k_RecordIndexColumn].title = s_RecordIndex;
            recordsList.columns[k_RecordIndexColumn].bindCell = (element, index) => BindCell(element, index, GetRecordIndexText);
            recordsList.columns[k_RecordTypeColumn].title = s_RecordType;
            recordsList.columns[k_RecordTypeColumn].bindCell = (element, index) => BindCell(element, index, GetRecordTypeText);
            recordsList.columns[k_SummaryColumn].title = s_Summary;
            recordsList.columns[k_SummaryColumn].bindCell = (element, index) => BindCell(element, index, GetSummaryText);
            recordsList.columns[k_FrameIndexColumn].title = s_FrameIndex;
            recordsList.columns[k_FrameIndexColumn].bindCell = (element, index) => BindCell(element, index, GetFrameIndexText);
            recordsList.columns[k_WorldColumn].title = s_World;
            recordsList.columns[k_WorldColumn].bindCell = (element, index) => BindCell(element, index, GetWorldName);
            recordsList.columns[k_ExecutingSystemColumn].title = s_ExecutingSystem;
            recordsList.columns[k_ExecutingSystemColumn].bindCell = (element, index) => BindCell(element, index, GetExecutingSystemName);
            recordsList.columns[k_OriginSystemColumn].title = s_OriginSystem;
            recordsList.columns[k_OriginSystemColumn].bindCell = (element, index) => BindCell(element, index, GetOriginSystemName);
            recordsList.columns[k_EntitiesColumn].title = s_Entities;
            recordsList.columns[k_EntitiesColumn].bindCell = (element, index) => BindCell(element, index, GetEntitiesText);
            recordsList.columns[k_ComponentsColumn].title = s_Components;
            recordsList.columns[k_ComponentsColumn].bindCell = (element, index) => BindCell(element, index, GetComponentTypesText);
            recordsList.selectionChanged += (objects) =>
            {
                m_Details.SetRecord((RecordView)objects.FirstOrDefault());
            };

            m_RecordsList = new SearchableList<MultiColumnListView, RecordView>(
                recordsList,
                searchElement,
                () => m_Records,
                (record) =>
                {
                    return new string[]
                    {
                        record.Index.ToString(),
                        record.RecordType.ToString(),
                        record.FrameIndex.ToString(),
                        record.World.Name,
                        record.ExecutingSystem.Name,
                        record.OriginSystem.Name,
                        GetRecordDataSystemName(record)
                    }
                    .Concat(record.Entities.Select(e => e.Name))
                    .Concat(record.ComponentTypes.Select(t => t.Name));
                });
            m_RecordsList.AddDefaultEnumerableOperatorHandlers();

            m_Details = new EntitiesJournalingWindowDetails(this, m_ContentContainer.Q("details"));
            m_Details.SetVisibility(false, true);

            var footer = window.Q("footer", "entities-journaling-window__footer");
            var footerLeft = footer.Q("footer-left");
            m_SearchResultLabel = footerLeft.Q<Label>("search-result");

            var footerRight = footer.Q("footer-right");
            m_RecordCountLabel = footerRight.Q<Label>("record-count");
            m_UsedBytesLabel = footerRight.Q<Label>("used-bytes");

            rootVisualElement.Add(window);

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Refresh();
        }

        protected override void OnCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            m_RecordsList = null;
            m_Records = null;
            m_WorldProxyManager.Dispose();
            m_WorldProxyManager = null;
        }

        protected override void OnUpdate()
        {
            var isRecording = Enabled;
            m_RecordToggle.value = isRecording;
            m_RecordCountLabel.text = string.Format(s_Records, FormattingUtility.CountToString(RecordCount));
            m_UsedBytesLabel.text = string.Format(s_MemoryUsed, FormattingUtility.BytesToString(UsedBytes), FormattingUtility.BytesToString(AllocatedBytes));
            m_SearchResultLabel.text = m_RecordsList.HasFilter ? string.Format(s_SearchResult, FormattingUtility.CountToString(m_RecordsList.Count)) : string.Empty;
            m_ContentContainer.SetVisibility(!isRecording && m_NeedPostProcess == 0);
            m_MessageContainer.SetVisibility(isRecording || m_NeedPostProcess > 0);
            m_MessageLabel.text = isRecording ? s_RecordingMessage : m_NeedPostProcess > 0 ? s_PostProcessingMessage : string.Empty;
            m_StopButton.SetVisibility(isRecording);

            if (m_NeedPostProcess > 0)
            {
                m_NeedPostProcess--;
                if (m_NeedPostProcess == 0 && m_Records.IsValid)
                {
                    m_Records.RunPostProcess();
                    m_RecordsList.Refresh();
                }
            }
        }

        protected override void OnWorldSelected(World world)
        {
        }

        void OnInitialTwoPaneSplitViewGeometryChangedEvent(GeometryChangedEvent e)
        {
            // This won't work in window OnEnable, have to postpone it here
            m_SplitPane.CollapseChild(1);
            m_SplitPane.UnregisterCallback<GeometryChangedEvent>(OnInitialTwoPaneSplitViewGeometryChangedEvent);
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    m_Details.SetRecord(RecordView.Null);
                    Refresh();
                    break;
            }
        }

        void Refresh()
        {
            m_WorldProxyManager.CreateWorldProxiesForAllWorlds();

            var isRecording = Enabled;
            if (isRecording)
            {
                m_Records = null;
                m_Details.SetRecord(RecordView.Null);
            }
            else
            {
                m_Records = new ReadOnlyRecordViewList(GetRecords(Ordering.Descending));
                if (Preferences.PostProcess && m_Records.Count > 0)
                {
                    m_NeedPostProcess = 2;
                    return;
                }
            }

            m_RecordsList.Refresh();
        }

        void AppendSearch(string token, string value)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(value))
                return;

            var search = token + "=" + value.DoubleQuoted(true);
            if (string.IsNullOrEmpty(m_RecordsList.SearchElement.value))
                m_RecordsList.SearchElement.value = search;
            else
                m_RecordsList.SearchElement.value += " " + search;
        }

        void BindCell(VisualElement element, int index, Func<RecordView, string> getValue)
        {
            var label = element as Label;
            if (label == null)
                throw new NullReferenceException(nameof(label));

            var record = m_RecordsList[index];
            var value = getValue(record);
            label.text = value;
        }

        internal string[] GetComponentDataText(RecordView record)
        {
            return TryGetRecordDataAsComponentDataArrayBoxed(record, out var componentDataArray) ? GetComponentDataValues(componentDataArray) : Array.Empty<string>();
        }

        string[] GetComponentDataValues(object componentDataArray)
        {
            PropertyContainer.Accept(m_ComponentDataValuesVisitor, componentDataArray);
            return m_ComponentDataValuesVisitor.GetValues();
        }

        internal static string GetSummaryText(RecordView record)
        {
            switch (record.RecordType)
            {
                case RecordType.WorldCreated:
                case RecordType.WorldDestroyed:
                    return GetWorldName(record);

                case RecordType.SystemAdded:
                case RecordType.SystemRemoved:
                    return GetRecordDataSystemName(record);

                case RecordType.CreateEntity:
                case RecordType.DestroyEntity:
                    return GetEntitiesText(record);

                case RecordType.AddComponent:
                case RecordType.RemoveComponent:
                case RecordType.EnableComponent:
                case RecordType.DisableComponent:
                case RecordType.SetComponentData:
                case RecordType.SetSharedComponentData:
                case RecordType.SetComponentObject:
                case RecordType.SetBuffer:
                case RecordType.GetComponentDataRW:
                case RecordType.GetComponentObjectRW:
                case RecordType.GetBufferRW:
                    return GetComponentTypesText(record);

                case RecordType.BakingRecord:
                    return string.Empty;

                default:
                    throw new NotImplementedException(record.RecordType.ToString());
            }
        }

        internal static string GetRecordIndexText(RecordView record) => FormattingUtility.CountToString(record.Index);
        internal static string GetRecordTypeText(RecordView record) => record.RecordType.ToString();
        internal static string GetFrameIndexText(RecordView record) => FormattingUtility.CountToString(record.FrameIndex);
        internal static string GetWorldName(RecordView record) => record.World.Name;
        internal static string GetExecutingSystemName(RecordView record) => record.ExecutingSystem.Name;
        internal static string GetOriginSystemName(RecordView record) => record.OriginSystem.Name;

        internal static string GetEntitiesText(RecordView record)
        {
            var entities = record.Entities;
            if (entities.Length == 0)
                return string.Empty;

            var text = entities[0].Name;
            if (entities.Length > 1)
                text += $" (+{entities.Length - 1})";

            return text;
        }

        internal static string GetComponentTypesText(RecordView record)
        {
            var componentTypes = record.ComponentTypes;
            if (componentTypes.Length == 0)
                return string.Empty;

            var text = componentTypes[0].Name;
            if (componentTypes.Length > 1)
                text += $" (+{componentTypes.Length - 1})";

            return text;
        }

        internal static string GetRecordDataSystemName(RecordView record)
        {
            return TryGetRecordDataAsSystemView(record, out var systemView) ? systemView.Name : string.Empty;
        }
    }
}
#endif
