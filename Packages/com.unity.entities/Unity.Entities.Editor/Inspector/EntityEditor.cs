using JetBrains.Annotations;
using System;
using System.Linq;
using Unity.Mathematics;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Assertions;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// This declares a custom inspector for the <see cref="EntitySelectionProxy"/> that can be used to override the
    /// default inspector.
    /// </summary>
    class EntityEditor : UnityEditor.Editor, IBinding
    {
        const int k_ContentGenerationDelayInMs = 15;

        // internal for tests
        internal readonly EntityInspectorContext m_InspectorContext = new EntityInspectorContext();

        BindableElement m_Root;
        bool m_Initialized;

        [SerializeField]
        EntitySelectionProxy m_SelectionContext;

        // Internal for test to avoid delaying the initialization
        internal bool ImmediateInitialize { get; set; }

        [UsedImplicitly]
        internal void InternalSetContextObject(UnityObject context)
        {
            m_SelectionContext = context as EntitySelectionProxy;
        }

        EntitySelectionProxy GetTargetProxy()
        {
            return target as EntitySelectionProxy ?? m_SelectionContext;
        }

        void OnEnable()
        {
            m_Root = new BindableElement { name = "Entity Inspector", binding = this };
            m_Root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        void OnDestroy()
        {
            if (!m_Initialized)
                return;

            m_Initialized = false;

            var targetProxy = GetTargetProxy();
            if (targetProxy != null)
                targetProxy.Release();
        }

        static EntityEditor()
        {
            TypeConversion.Register((ref float3 v) => (Vector3) v);
            TypeConversion.Register((ref Vector3 v) => (float3) v);

            TypeConversion.Register((ref float4 v) => (Vector4) v);
            TypeConversion.Register((ref Vector4 v) => (float4) v);

            TypeConversion.Register<quaternion, Vector3>((ref quaternion v) => ((Quaternion)v).eulerAngles);
            TypeConversion.Register<Vector3, quaternion>((ref Vector3 v) => quaternion.Euler(math.radians(v)));
        }

        void OnBeforeAssemblyReload()
        {
            // After a domain reload, it's impossible to retrieve the Entity this editor is
            // referring to. So before the domain is unloaded, we ensure that:
            // 1. The active selection or context is not an EntitySelectionProxy, so we don't try to
            // re-select it once the domain is reloaded.
            if (Selection.activeObject is EntitySelectionProxy || Selection.activeContext is EntitySelectionProxy)
                Selection.activeObject = null; // Note that changing the selection also clears the active context

            // 2. This editor no longer exists, so a locked inspector is not revived with
            // invalid data once the domain is reloaded.
            DestroyImmediate(this);
        }

        public override bool UseDefaultMargins() => false;

        protected override void OnHeaderGUI()
        {
            // Intentionally overriden to avoid the default header to be drawn.
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (m_Initialized)
                return m_Root;

            m_Initialized = true;

            Resources.AddCommonVariables(m_Root);
            Resources.Templates.Inspector.InspectorStyle.AddStyles(m_Root);
            Resources.Templates.Inspector.EntityInspector.AddStyles(m_Root);
            Resources.Templates.DotsEditorCommon.AddStyles(m_Root);

            var actualTarget = GetTargetProxy();

            Assert.IsNotNull(actualTarget, "Neither the target or the selection context is valid for this inspector.");

            Initialize(actualTarget);
            return m_Root;
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var inspectorRoot = m_Root.parent.parent;
            var header = (IMGUIContainer) inspectorRoot.Children().First();
            header.MarkDirtyLayout();
            m_Root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        void IBinding.PreUpdate()
        {
            if (m_InspectorContext.TargetExists()) return;
            m_Root.Clear();
        }

        void IBinding.Update()
        {
            // Nothing to do
        }

        void IBinding.Release()
        {
            // Nothing to do
        }

        void Initialize(EntitySelectionProxy proxy)
        {
            proxy.Retain();

            m_InspectorContext.SetContext(proxy);
            m_Root.Clear();
            m_Root.AddToClassList(UssClasses.Inspector.EntityInspector);

            var header = new EntityHeader(m_InspectorContext);
            var p = new PropertyElement();
            p.SetTarget(header);
            m_Root.Add(p);

            var label = new CenteredMessageElement { Message = "Loading ..." };
            m_Root.Add(label);

            if (ImmediateInitialize)
                InitializeContent();
            else
                m_Root.schedule.Execute(InitializeContent).StartingIn(k_ContentGenerationDelayInMs);

            void InitializeContent()
            {
                label.RemoveFromHierarchy();
                var presenter = new PropertyElement();
                var t = new EntityInspectorContent(m_InspectorContext);
                presenter.SetTarget(t);
                m_Root.Add(presenter);
                m_Root.ForceUpdateBindings();
            }
        }

        internal readonly struct EntityInspectorContent
        {
            [CreateProperty, TabView("EntityInspector"), UsedImplicitly]
            public ITabContent[] Tabs { get; }

            public EntityInspectorContent(EntityInspectorContext entityInspectorContext)
            {
                Tabs = new ITabContent[]
                {
                    new ComponentsTab(entityInspectorContext),
                    new AspectsTab(entityInspectorContext),
                    new RelationshipsTab(entityInspectorContext)
                };
            }
        }
    }
}
