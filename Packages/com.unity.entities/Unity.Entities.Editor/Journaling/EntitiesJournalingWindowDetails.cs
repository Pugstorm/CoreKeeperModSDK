#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.Entities.EntitiesJournaling;

namespace Unity.Entities.Editor
{
    partial class EntitiesJournalingWindow
    {
        class EntitiesJournalingWindowDetails
        {
            static readonly string s_WorldSeqNum = L10n.Tr("Sequence Number");
            static readonly string s_SystemHandle = L10n.Tr("Handle");
            static readonly string s_SystemVersion = L10n.Tr("Version");

            static readonly Lazy<Texture2D> s_WorldIcon = new Lazy<Texture2D>(() => PackageResources.LoadIcon("World/World.png"));
            static readonly Lazy<Texture2D> s_SystemIcon = new Lazy<Texture2D>(() => PackageResources.LoadIcon("System/System.png"));
            static readonly Lazy<Texture2D> s_EntityIcon = new Lazy<Texture2D>(() => PackageResources.LoadIcon("Entity/Entity.png"));
            static readonly Lazy<Texture2D> s_ComponentIcon = new Lazy<Texture2D>(() => PackageResources.LoadIcon("Components/Component.png"));
            static readonly VisualElementTemplate s_DetailsTemplate = PackageResources.LoadTemplate("Journaling/entities-journaling-details");
            static readonly VisualElementTemplate s_DetailsEntityTemplate = PackageResources.LoadTemplate("Journaling/entities-journaling-details-entity");
            static readonly VisualElementTemplate s_DetailsComponentTemplate = PackageResources.LoadTemplate("Journaling/entities-journaling-details-component");
            static readonly VisualElementTemplate s_SearchAndGotoButtons = PackageResources.LoadTemplate("Journaling/search-and-goto-buttons");
            static readonly StyleSheetWithVariant s_ComponentHeader = PackageResources.LoadStyleSheet("Inspector/component-header");
            static readonly StyleSheetWithVariant s_Variables = PackageResources.LoadStyleSheet("Common/variables");

            readonly EntitiesJournalingWindow m_Window;
            readonly SearchableList<ListView, EntitiesJournaling.EntityView> m_EntitiesList;
            readonly SearchableList<ListView, ComponentTypeView> m_ComponentsList;
            RecordView m_Record;

            readonly VisualElement m_Root;
            readonly VisualElement m_Header;
            readonly VisualElement m_Content;
            readonly Image m_HeaderIcon;
            readonly Label m_HeaderTitle;
            readonly TextField m_RecordIndex;
            readonly TextField m_FrameIndex;
            readonly TextField m_World;
            readonly TextField m_ExecutingSystem;
            readonly Button m_ExecutingSystemSearchButton;
            readonly Button m_ExecutingSystemGotoButton;
            readonly TextField m_OriginSystem;
            readonly Button m_OriginSystemSearchButton;
            readonly Button m_OriginSystemGotoButton;
            readonly VisualElement m_ContentWorld;
            readonly TextField m_ContentWorldName;
            readonly TextField m_ContentWorldSeqNum;
            readonly VisualElement m_ContentSystem;
            readonly TextField m_ContentSystemName;
            readonly TextField m_ContentSystemHandle;
            readonly TextField m_ContentSystemVersion;
            readonly Button m_ContentSystemGotoButton;
            readonly VisualElement m_ContentTabs;
            readonly TabContent m_EntitiesTabContent;
            readonly TwoPaneSplitView m_EntitiesTabContentSplitPane;
            readonly VisualElement m_EntitiesTabContentTop;
            readonly VisualElement m_EntitiesTabContentBottom;
            readonly VisualElement m_EntitiesFooter;
            readonly Foldout m_EntitiesFooterFoldout;
            readonly PropertyElement m_EntitiesFooterProperty;
            readonly TabContent m_ComponentsTabContent;

            public EntitiesJournalingWindowDetails(EntitiesJournalingWindow window, VisualElement root)
            {
                m_Window = window;
                m_Root = s_DetailsTemplate.Clone(root);
                m_Header = m_Root.Q("header");

                var headerTop = m_Header.Q("top");
                m_HeaderIcon = headerTop.Q<Image>("icon");
                m_HeaderTitle = headerTop.Q<Label>("title");

                var headerBottom = m_Header.Q("bottom");
                var recordIndex = headerBottom.Q("record-index");
                recordIndex.Q<Label>("record-index-label").text = s_RecordIndex;
                m_RecordIndex = recordIndex.Q<TextField>("record-index-value");
                m_RecordIndex.isReadOnly = true;

                var frameIndex = headerBottom.Q("frame-index");
                frameIndex.Q<Label>("frame-index-label").text = s_FrameIndex;
                m_FrameIndex = frameIndex.Q<TextField>("frame-index-value");
                m_FrameIndex.isReadOnly = true;

                var world = headerBottom.Q("world");
                world.Q<Label>("world-label").text = s_World;
                m_World = world.Q<TextField>("world-value");
                m_World.isReadOnly = true;

                var executingSystem = headerBottom.Q("executing-system");
                executingSystem.Q<Label>("executing-system-label").text = s_ExecutingSystem;
                m_ExecutingSystem = executingSystem.Q<TextField>("executing-system-value");
                m_ExecutingSystem.isReadOnly = true;
                m_ExecutingSystemSearchButton = headerBottom.Q<Button>("executing-system-search");
                m_ExecutingSystemSearchButton.clicked += () => SearchExecutingSystem();
                m_ExecutingSystemGotoButton = headerBottom.Q<Button>("executing-system-goto");
                m_ExecutingSystemGotoButton.Q<Button>("executing-system-goto").clicked += () => SelectExecutingSystem();

                var originSystem = headerBottom.Q("origin-system");
                originSystem.Q<Label>("origin-system-label").text = s_OriginSystem;
                m_OriginSystem = originSystem.Q<TextField>("origin-system-value");
                m_OriginSystem.isReadOnly = true;
                m_OriginSystemSearchButton = headerBottom.Q<Button>("origin-system-search");
                m_OriginSystemSearchButton.clicked += () => SearchOriginSystem();
                m_OriginSystemGotoButton = headerBottom.Q<Button>("origin-system-goto");
                m_OriginSystemGotoButton.clicked += () => SelectOriginSystem();

                m_Content = m_Root.Q("content");
                m_ContentWorld = m_Content.Q("content-world");
                m_ContentSystem = m_Content.Q("content-system");
                m_ContentTabs = m_Content.Q("content-tabs");

                m_ContentWorld.Q<Label>("content-world-name-label").text = s_World;
                m_ContentWorldName = m_ContentWorld.Q<TextField>("content-world-name-value");

                m_ContentWorld.Q<Label>("content-world-seqnum-label").text = s_WorldSeqNum;
                m_ContentWorldSeqNum = m_ContentWorld.Q<TextField>("content-world-seqnum-value");

                m_ContentSystem.Q<Label>("content-system-name-label").text = s_System;
                m_ContentSystemName = m_ContentSystem.Q<TextField>("content-system-name-value");
                m_ContentSystem.Q<Button>("content-system-search").clicked += () => SearchSystem();
                m_ContentSystemGotoButton = m_ContentSystem.Q<Button>("content-system-goto");
                m_ContentSystemGotoButton.clicked += () => SelectSystem();

                m_ContentSystem.Q<Label>("content-system-handle-label").text = s_SystemHandle;
                m_ContentSystemHandle = m_ContentSystem.Q<TextField>("content-system-handle-value");

                m_ContentSystem.Q<Label>("content-system-version-label").text = s_SystemVersion;
                m_ContentSystemVersion = m_ContentSystem.Q<TextField>("content-system-version-value");

                m_EntitiesTabContent = m_ContentTabs.Q<TabContent>("entities-tab");
                m_EntitiesTabContent.TabName = s_Entities;
                m_EntitiesTabContentSplitPane = m_EntitiesTabContent.Q<TwoPaneSplitView>("split-pane");
                m_EntitiesTabContentSplitPane.RegisterCallback<GeometryChangedEvent>(OnInitialTwoPaneSplitViewGeometryChangedEvent);
                m_EntitiesTabContentTop = m_EntitiesTabContent.Q("entities-tab-top");
                m_EntitiesTabContentBottom = m_EntitiesTabContent.Q("entities-tab-bottom");
                m_EntitiesTabContentBottom.RegisterCallback<GeometryChangedEvent>((e) =>
                {
                    StylingUtility.AlignInspectorLabelWidth(m_EntitiesTabContentBottom);
                });

                var entitiesList = m_EntitiesTabContent.Q<ListView>("entities-list");
                entitiesList.fixedItemHeight = 20;
                entitiesList.makeItem = () => s_DetailsEntityTemplate.Clone();
                entitiesList.bindItem = (element, index) => BindItem(element, index, GetEntityName, SearchEntity, SelectEntity, EntityExist);
                entitiesList.selectionChanged += (items) =>
                {
                    if (items == null || !items.Any())
                    {
                        SetEntitiesFooterData(null);
                        return;
                    }

                    var entity = (EntitiesJournaling.EntityView)items.First();
                    var data = GetEntityComponentData(entity);
                    SetEntitiesFooterData(data);
                };

                var entitiesSearch = m_EntitiesTabContent.Q<SearchElement>("entities-search");
                m_EntitiesList = new SearchableList<ListView, EntitiesJournaling.EntityView>(
                    entitiesList,
                    entitiesSearch,
                    () => new ReadOnlyEntityViewList(m_Record.Entities),
                    (e) => new[] { e.Name });
                entitiesSearch.AddSearchFilterCallbackWithPopupItem<EntitiesJournaling.EntityView, int>(k_EntityIndexToken, e => e.Index, s_EntityIndex);
                entitiesSearch.AddSearchFilterCallbackWithPopupItem<EntitiesJournaling.EntityView, IEnumerable<string>>(k_ComponentDataValueToken, e => m_Window.GetComponentDataValues(GetEntityComponentData(e)), s_ComponentDataValue);

                m_EntitiesFooter = m_EntitiesTabContent.Q("entities-footer");
                m_EntitiesFooterFoldout = m_EntitiesFooter.Q<Foldout>("entities-footer-foldout");
                m_EntitiesFooterProperty = m_EntitiesFooter.Q<PropertyElement>("entities-footer-property");
                s_ComponentHeader.AddStyles(m_EntitiesFooterFoldout);
                s_Variables.AddStyles(m_EntitiesFooterFoldout);

                var entitiesFooterFoldoutButtons = s_SearchAndGotoButtons.Clone();
                entitiesFooterFoldoutButtons.style.marginRight = 5;
                m_EntitiesFooterFoldout.Q<Toggle>().Add(entitiesFooterFoldoutButtons);

                m_ComponentsTabContent = m_ContentTabs.Q<TabContent>("components-tab");
                m_ComponentsTabContent.TabName = s_Components;

                var componentsList = m_ComponentsTabContent.Q<ListView>("components-list");
                componentsList.fixedItemHeight = 20;
                componentsList.makeItem = () => s_DetailsComponentTemplate.Clone();
                componentsList.bindItem = (element, index) => BindItem(element, index, GetComponentTypeName, SearchComponent, SelectComponent, ComponentExist);

                var componentsSearch = m_ComponentsTabContent.Q<SearchElement>("components-search");
                m_ComponentsList = new SearchableList<ListView, ComponentTypeView>(
                    componentsList,
                    componentsSearch,
                    () => new ReadOnlyComponentTypeViewList(m_Record.ComponentTypes),
                    (c) => new[] { c.Name });

                SetEntitiesFooterVisibility(false);
            }

            public void SetVisibility(bool isVisible, bool force = false)
            {
                if (!force && m_Root.IsVisible() == isVisible)
                    return;

                if (isVisible)
                    m_Window.m_SplitPane.UnCollapse();
                else
                    m_Window.m_SplitPane.CollapseChild(1);

                m_Root.SetVisibility(isVisible);
            }

            public void ToggleVisibility()
            {
                if (m_Root.IsVisible())
                {
                    m_Window.m_SplitPane.CollapseChild(1);
                    m_Root.SetVisibility(false);
                }
                else
                {
                    m_Root.SetVisibility(true);
                    m_Window.m_SplitPane.UnCollapse();
                }
            }

            public void SetRecord(RecordView record)
            {
                m_Record = record;
                if (m_Record == RecordView.Null)
                {
                    m_Header.SetVisibility(false);
                    m_Content.SetVisibility(false);
                    m_ContentWorld.SetVisibility(false);
                    m_ContentSystem.SetVisibility(false);
                    m_ContentTabs.SetVisibility(false);
                    SetEntitiesFooterVisibility(false);
                    SetVisibility(false);
                }
                else
                {
                    var hasExecutingSystem = record.World.Reference != null && record.ExecutingSystem.Handle != default;
                    var hasOriginSystem = record.World.Reference != null && record.OriginSystem.Handle != default;

                    // Header
                    switch (record.RecordType)
                    {
                        case RecordType.WorldCreated:
                        case RecordType.WorldDestroyed:
                            m_HeaderIcon.image = s_WorldIcon.Value;
                            break;

                        case RecordType.SystemAdded:
                        case RecordType.SystemRemoved:
                            m_HeaderIcon.image = s_SystemIcon.Value;
                            break;

                        case RecordType.CreateEntity:
                        case RecordType.DestroyEntity:
                            m_HeaderIcon.image = s_EntityIcon.Value;
                            break;

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
                            m_HeaderIcon.image = s_ComponentIcon.Value;
                            break;

                        case RecordType.BakingRecord:
                            m_HeaderIcon.image = null;
                            break;

                        default:
                            throw new NotImplementedException(record.RecordType.ToString());
                    }
                    m_HeaderTitle.text = record.RecordType.ToString();
                    m_RecordIndex.value = record.Index.ToString();
                    m_FrameIndex.value = record.FrameIndex.ToString();
                    m_World.value = record.World.Name;
                    m_ExecutingSystem.value = record.ExecutingSystem.Name;
                    m_ExecutingSystemSearchButton.SetEnabled(hasExecutingSystem);
                    m_ExecutingSystemGotoButton.SetEnabled(hasExecutingSystem);
                    m_OriginSystem.value = record.OriginSystem.Name;
                    m_OriginSystemSearchButton.SetEnabled(hasOriginSystem);
                    m_OriginSystemGotoButton.SetEnabled(hasOriginSystem);
                    m_Header.SetVisibility(true);

                    // Content
                    switch (record.RecordType)
                    {
                        case RecordType.WorldCreated:
                        case RecordType.WorldDestroyed:
                            m_ContentWorldName.value = record.World.Name;
                            m_ContentWorldSeqNum.value = record.World.SequenceNumber.ToString();
                            m_ContentWorld.SetVisibility(true);
                            m_ContentSystem.SetVisibility(false);
                            m_ContentTabs.SetVisibility(false);
                            SetEntitiesFooterVisibility(false);
                            break;

                        case RecordType.SystemAdded:
                        case RecordType.SystemRemoved:
                            TryGetRecordDataAsSystemView(record, out var systemView);
                            m_ContentSystemName.value = systemView.Name;
                            m_ContentSystemHandle.value = systemView.Handle.m_Handle.ToString();
                            m_ContentSystemVersion.value = systemView.Handle.m_Version.ToString();
                            m_ContentSystemGotoButton.SetEnabled(record.World.Reference != null && systemView.Handle != default);
                            m_ContentWorld.SetVisibility(false);
                            m_ContentSystem.SetVisibility(true);
                            m_ContentTabs.SetVisibility(false);
                            SetEntitiesFooterVisibility(false);
                            break;

                        case RecordType.CreateEntity:
                        case RecordType.DestroyEntity:
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
                            m_EntitiesTabContent.TabName = $"{s_Entities} {record.Entities.Length}";
                            m_ComponentsTabContent.TabName = $"{s_Components} {record.ComponentTypes.Length}";
                            m_ContentWorld.SetVisibility(false);
                            m_ContentSystem.SetVisibility(false);
                            m_ContentTabs.SetVisibility(true);
                            SetEntitiesFooterVisibility(false);
                            break;

                        case RecordType.BakingRecord:
                            m_ContentWorld.SetVisibility(false);
                            m_ContentSystem.SetVisibility(false);
                            m_ContentTabs.SetVisibility(false);
                            SetEntitiesFooterVisibility(false);
                            break;

                        default:
                            throw new NotImplementedException(record.RecordType.ToString());
                    }
                    m_EntitiesList.List.selectedIndex = -1;
                    m_EntitiesList.Refresh();
                    m_ComponentsList.List.selectedIndex = -1;
                    m_ComponentsList.Refresh();
                    m_Content.SetVisibility(true);

                    SetVisibility(true);
                }
            }

            void OnInitialTwoPaneSplitViewGeometryChangedEvent(GeometryChangedEvent e)
            {
                // This won't work in window OnEnable, have to postpone it here
                m_EntitiesTabContentSplitPane.CollapseChild(1);
                m_EntitiesTabContentSplitPane.UnregisterCallback<GeometryChangedEvent>(OnInitialTwoPaneSplitViewGeometryChangedEvent);
            }

            object GetEntityComponentData(EntitiesJournaling.EntityView entity)
            {
                if (TryGetRecordDataAsComponentDataArrayBoxed(m_Record, out var componentDataArray))
                {
                    var index = m_Record.Entities.IndexOf(entity);
                    return componentDataArray.GetValue(index);
                }
                return null;
            }

            void SetEntitiesFooterData(object data)
            {
                if (data == null)
                {
                    m_EntitiesFooterProperty.ClearTarget();
                    SetEntitiesFooterVisibility(false);
                }
                else
                {
                    var componentType = data.GetType();
                    var componentTypeIndex = TypeManager.GetTypeIndex(componentType);
                    var componentTypeName = componentType.Name;
                    m_EntitiesFooterFoldout.text = componentTypeName;
                    m_EntitiesFooterFoldout.Q<Button>("search-button").clickable = new Clickable(() => SearchComponent(componentTypeIndex.Index));
                    m_EntitiesFooterFoldout.Q<Button>("goto-button").clickable = new Clickable(() => SelectComponent(componentType));

                    // Important to set footer visible before changing target and aligning
                    SetEntitiesFooterVisibility(true);

                    m_EntitiesFooterProperty.SetTarget(data);
                    m_EntitiesFooterProperty.ForceReload();
                    StylingUtility.AlignInspectorLabelWidth(m_EntitiesTabContentBottom);
                }
            }

            void SetEntitiesFooterVisibility(bool isVisible)
            {
                if (m_EntitiesTabContentBottom.IsVisible() == isVisible)
                    return;

                if (isVisible)
                    m_EntitiesTabContentSplitPane.UnCollapse();
                else
                    m_EntitiesTabContentSplitPane.CollapseChild(1);

                m_EntitiesTabContentBottom.SetVisibility(isVisible);
            }

            void BindItem(VisualElement element, int index, Func<int, string> getValue, Action<int> search, Action<int> goTo, Func<int, bool> exist)
            {
                element.Q<Label>("name").text = getValue(index);
                element.Q<Button>("search").clickable = new Clickable(() => search(index));

                var gotoButton = element.Q<Button>("goto");
                if (exist(index))
                {
                    gotoButton.SetEnabled(true);
                    gotoButton.clickable = new Clickable(() => goTo(index));
                }
                else
                {
                    gotoButton.SetEnabled(false);
                    gotoButton.clickable = null;
                }
            }

            void SearchExecutingSystem()
            {
                if (m_Record == RecordView.Null)
                    return;

                var executingSystemName = m_Record.ExecutingSystem.Name;
                if (string.IsNullOrEmpty(executingSystemName))
                    return;

                m_Window.AppendSearch(k_ExecutingSystemToken, executingSystemName);
            }

            void SelectExecutingSystem()
            {
                if (m_Record == RecordView.Null)
                    return;

                SelectSystem(m_Record.World.Reference, m_Record.ExecutingSystem.Handle);
            }

            void SearchOriginSystem()
            {
                if (m_Record == RecordView.Null)
                    return;

                var originSystemName = m_Record.OriginSystem.Name;
                if (string.IsNullOrEmpty(originSystemName))
                    return;

                m_Window.AppendSearch(k_OriginSystemToken, originSystemName);
            }

            void SelectOriginSystem()
            {
                if (m_Record == RecordView.Null)
                    return;

                SelectSystem(m_Record.World.Reference, m_Record.OriginSystem.Handle);
            }

            void SearchSystem()
            {
                if (TryGetRecordDataAsSystemView(m_Record, out var systemView))
                {
                    var systemName = systemView.Name;
                    if (string.IsNullOrEmpty(systemName))
                        return;

                    m_Window.AppendSearch(k_SystemToken, systemName);
                }
            }

            void SelectSystem()
            {
                if (TryGetRecordDataAsSystemView(m_Record, out var systemView))
                    SelectSystem(m_Record.World.Reference, systemView.Handle);
            }

            void SelectSystem(World world, SystemHandle system)
            {
                if (world == null || system == default)
                    return;

                var worldProxy = m_Window.m_WorldProxyManager.GetWorldProxyForGivenWorld(world);
                var systemProxy = new SystemProxy(system, world, worldProxy);
                ContentUtilities.ShowSystemInspectorContent(systemProxy);
            }

            bool EntityExist(int index)
            {
                if (m_Record == RecordView.Null)
                    return false;

                return m_EntitiesList[index].Reference != Entity.Null;
            }

            string GetEntityName(int index)
            {
                return m_EntitiesList[index].Name;
            }

            string GetEntityIndex(int index)
            {
                return m_EntitiesList[index].Index.ToString();
            }

            void SearchEntity(int index)
            {
                m_Window.AppendSearch(k_EntityIndexToken, GetEntityIndex(index));
            }

            void SelectEntity(int index)
            {
                if (m_Record == RecordView.Null)
                    return;

                var world = m_Record.World.Reference;
                if (world == null)
                    return;

                var entity = m_EntitiesList[index].Reference;
                if (entity == Entity.Null)
                    return;

                var undoGroup = Undo.GetCurrentGroup();
                EntitySelectionProxy.SelectEntity(world, entity);
                Undo.CollapseUndoOperations(undoGroup);
            }

            bool ComponentExist(int index)
            {
                if (m_Record == RecordView.Null)
                    return false;

                return TypeManager.GetType(m_ComponentsList[index].TypeIndex) != null;
            }

            string GetComponentTypeName(int index)
            {
                return m_ComponentsList[index].Name;
            }

            string GetComponentTypeIndex(int index)
            {
                return m_ComponentsList[index].TypeIndex.Value.ToString();
            }

            void SearchComponent(int index)
            {
                m_Window.AppendSearch(k_ComponentTypeIndexToken, GetComponentTypeIndex(index));
            }

            void SelectComponent(int index)
            {
                SelectComponent(TypeManager.GetType(m_ComponentsList[index].TypeIndex));
            }

            void SelectComponent(Type componentType)
            {
                if (componentType == null)
                    return;

                ContentUtilities.ShowComponentInspectorContent(componentType);
            }
        }
    }
}
#endif
