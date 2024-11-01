using System;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemInformationVisualElement : BindableElement, IBinding
    {
        internal static readonly BasicPool<SystemInformationVisualElement> Pool = new BasicPool<SystemInformationVisualElement>(() => new SystemInformationVisualElement());

        SystemTreeViewItem m_Target;
        public SystemTreeView TreeView { get; set; }
        const float k_SystemNameLabelWidth = 100f;
        const float k_SingleIndentWidth = 15f;
        const string k_UnityTreeViewItemIndentsName = "unity-tree-view__item-indents";

        public SystemTreeViewItem Target
        {
            get => m_Target;
            set
            {
                if (m_Target == value)
                    return;
                m_Target = value;
                Update();
            }
        }

        readonly VisualElement m_SystemEnableToggleContainer;
        readonly Toggle m_SystemEnableToggle;
        readonly VisualElement m_Icon;
        readonly Label m_SystemNameLabel;
        readonly Label m_WorldNameLabel;
        readonly Label m_NamespaceLabel;
        readonly Label m_EntityCountLabel;
        readonly Label m_RunningTimeLabel;
        readonly VisualElement m_WorldNameColumn;
        readonly VisualElement m_NamespaceColumn;
        readonly VisualElement m_EntityCountColumn;
        readonly VisualElement m_TimeColumn;

        SystemInformationVisualElement()
        {
            Resources.Templates.SystemScheduleItem.Clone(this);
            Resources.Templates.SystemScheduleItem.AddStyles(this);
            Resources.Templates.SystemSchedule.AddStyles(this);

            binding = this;

            AddToClassList(UssClasses.DotsEditorCommon.CommonResources);

            m_SystemEnableToggleContainer = this.Q(className: UssClasses.SystemScheduleWindow.Items.EnabledContainer);
            m_SystemEnableToggle = this.Q<Toggle>(className: UssClasses.SystemScheduleWindow.Items.StateToggle);
            m_SystemEnableToggle.RegisterValueChangedCallback(OnSystemTogglePress);

            m_Icon = this.Q(className: UssClasses.SystemScheduleWindow.Items.Icon);

            m_SystemNameLabel = this.Q<Label>(className: UssClasses.SystemScheduleWindow.Items.SystemName);
            m_WorldNameLabel = this.Q<Label>(className: UssClasses.SystemScheduleWindow.Items.WorldName);
            m_NamespaceLabel = this.Q<Label>(className: UssClasses.SystemScheduleWindow.Items.Namespace);
            m_EntityCountLabel = this.Q<Label>(className: UssClasses.SystemScheduleWindow.Items.Matches);
            m_RunningTimeLabel = this.Q<Label>(className: UssClasses.SystemScheduleWindow.Items.Time);

            m_WorldNameColumn = this.Q(className: UssClasses.SystemScheduleWindow.Items.WorldNameColumn);
            m_NamespaceColumn = this.Q(className: UssClasses.SystemScheduleWindow.Items.NamespaceColumn);
            m_EntityCountColumn = this.Q(className: UssClasses.SystemScheduleWindow.Items.EntityCountColumn);
            m_TimeColumn = this.Q(className: UssClasses.SystemScheduleWindow.Items.TimeColumn);
        }

        public static SystemInformationVisualElement Acquire(SystemTreeView treeView)
        {
            var item = Pool.Acquire();

            item.TreeView = treeView;
            return item;
        }

        public void Release()
        {
            Target = null;
            TreeView = null;
            Pool.Release(this);
        }

        static void SetText(Label label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        public void Update()
        {
            if (null == Target)
                return;

            if (Target.SystemProxy.Valid && Target.SystemProxy.World == null)
                return;

            InsertSystemToggle();

            m_Icon.style.display = string.Empty == GetSystemClass(Target.SystemProxy) ? DisplayStyle.None : DisplayStyle.Flex;
            SetText(m_SystemNameLabel, Target.GetSystemName());
            SetSystemNameLabelWidth(m_SystemNameLabel);

            // world name column
            var worldName = Target.GetWorldName();
            SetText(m_WorldNameLabel, worldName);
            m_WorldNameColumn.SetVisibility(TreeView.ShowWorldColumn);

            // namespace column
            var namespaceString = Target.GetNamespace();
            SetText(m_NamespaceLabel, namespaceString);
            m_NamespaceColumn.SetVisibility(TreeView.ShowNamespaceColumn);

            // entity count column
            var entityCount = Target.GetEntityMatches();
            SetText(m_EntityCountLabel, entityCount);
            m_EntityCountColumn.SetVisibility(TreeView.ShowEntityCountColumn);
            if (!TreeView.Show0sInEntityCountAndTimeColumn && entityCount.Equals("0"))
            {
                m_EntityCountLabel.Hide();
            }
            else
            {
                m_EntityCountLabel.Show();
            }

            // runtime column
            var runningTime = Target.GetRunningTime(TreeView.ShowMorePrecisionForRunningTime);
            SetText(m_RunningTimeLabel, runningTime);
            m_TimeColumn.SetVisibility(TreeView.ShowTimeColumn);
            if (!TreeView.Show0sInEntityCountAndTimeColumn &&
                (runningTime.Equals("0.00") || runningTime.Equals("0.0000")))
            {
                m_RunningTimeLabel.Hide();
            }
            else
            {
                m_RunningTimeLabel.Show();
            }

            SetSystemClass(m_Icon, Target.SystemProxy);
            SetGroupNodeLabelBold(m_SystemNameLabel, Target.SystemProxy);

            if (!Target.SystemProxy.Valid) // player loop system without children
            {
                SetEnabled(Target.HasChildren);

                m_SystemEnableToggle.style.display = DisplayStyle.Flex;
                m_SystemEnableToggle.SetEnabled(false);
                SetSystemToggleState();

                m_SystemNameLabel.SetEnabled(true);
            }
            else
            {
                SetEnabled(true);
                m_SystemEnableToggle.style.display = DisplayStyle.Flex;

                var systemState = Target.SystemProxy.Enabled;
                m_SystemEnableToggle.value = systemState;
                SetSystemToggleState();

                var groupState = systemState && Target.GetParentState();
                m_SystemEnableToggle.SetEnabled(true);
                m_SystemNameLabel.SetEnabled(groupState);
                m_WorldNameLabel.SetEnabled(groupState);
                m_NamespaceLabel.SetEnabled(groupState);
                m_EntityCountLabel.SetEnabled(groupState);
                m_RunningTimeLabel.SetEnabled(groupState);
            }
        }

        void InsertSystemToggle()
        {
            var itemRoot = parent?.parent;

            switch (TreeView.SearchFilter.IsEmpty)
            {
                case true when itemRoot != null && itemRoot.IndexOf(m_SystemEnableToggleContainer) == -1:
                    itemRoot.Insert(0, m_SystemEnableToggleContainer);
                    break;
                case false when !Contains(m_SystemEnableToggleContainer):
                    this.Q(classes: UssClasses.SystemScheduleWindow.Items.SystemNameColumn)?.Insert(0, m_SystemEnableToggleContainer);
                    break;
            }
        }

        void SetSystemToggleState()
        {
            switch (Target.GetSystemToggleState())
            {
                case SystemTreeViewItem.SystemToggleState.Disabled:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, false);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, false);
                    break;
                case SystemTreeViewItem.SystemToggleState.Mixed:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, false);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, true);
                    break;
                case SystemTreeViewItem.SystemToggleState.AllEnabled:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, true);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, false);
                    break;
                default:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, true);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, false);
                    break;
            }
        }

        void SetSystemNameLabelWidth(VisualElement label)
        {
            var treeViewItemVisualElement = parent?.parent;
            var itemIndentsContainerName = treeViewItemVisualElement?.Q(k_UnityTreeViewItemIndentsName);
            if (itemIndentsContainerName == null)
            {
                label.style.width = k_SystemNameLabelWidth;
            }
            else
            {
                var indentWidth = itemIndentsContainerName.childCount * k_SingleIndentWidth;
                label.style.width = k_SystemNameLabelWidth - indentWidth;
                itemIndentsContainerName.style.width = indentWidth;
            }
        }

        static void SetSystemClass(VisualElement element, SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;

            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.BeginCommandBufferIcon,
                (flags & SystemCategory.ECBSystemBegin) != 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.EndCommandBufferIcon,
                (flags & SystemCategory.ECBSystemEnd) != 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.UnmanagedSystemIcon,
                (flags & SystemCategory.Unmanaged) != 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.SystemIcon,
                (flags & SystemCategory.SystemBase) != 0 && (flags & SystemCategory.EntityCommandBufferSystem) == 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.SystemGroupIcon,
                (flags & SystemCategory.SystemGroup) != 0);
        }

        static void SetGroupNodeLabelBold(VisualElement element, SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;

            var isBold = flags == 0 || (flags & SystemCategory.SystemGroup) != 0;
            element.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemNameBold, isBold);
            element.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemNameNormal, !isBold);
        }

        static string GetSystemClass(SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;

            if ((flags & SystemCategory.ECBSystemBegin) != 0)
                return UssClasses.SystemScheduleWindow.Items.BeginCommandBufferIcon;
            if ((flags & SystemCategory.ECBSystemEnd) != 0)
                return UssClasses.SystemScheduleWindow.Items.EndCommandBufferIcon;
            if ((flags & SystemCategory.EntityCommandBufferSystem) != 0)
                return string.Empty;
            if ((flags & SystemCategory.Unmanaged) != 0)
                return UssClasses.SystemScheduleWindow.Items.UnmanagedSystemIcon;
            if ((flags & SystemCategory.SystemGroup) != 0)
                return UssClasses.SystemScheduleWindow.Items.SystemGroupIcon;
            if ((flags & SystemCategory.SystemBase) != 0)
                return UssClasses.SystemScheduleWindow.Items.SystemIcon;

            return string.Empty;
        }

        void OnSystemTogglePress(ChangeEvent<bool> evt)
        {
            if (!Target.SystemProxy.Valid)
                return;

            Target.SetSystemEnabled(evt.newValue);
            // Update to reflect the toggle state right away in the UI.
            Update();

#if !UNITY_2023_2_OR_NEWER
            evt.PreventDefault();
#endif
            evt.StopPropagation();
        }

        void IBinding.PreUpdate() { }

        void IBinding.Release() { }
    }
}
