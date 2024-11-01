using System;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    class BindingTarget<TTarget> : IBindingTarget<TTarget>
    {
        class ReloadAtPathVisitor : PathVisitor, IResetableVisitor
        {
            public BindingContextElement Binding;
            public VisualElement Element;

            public override void Reset()
            {
                base.Reset();
                Binding = null;
                Element = null;
            }

            protected override void VisitPath<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                Binding.SwapWithInstance(Path, Element, value);
            }
        }

        TTarget m_Target;
        readonly InspectorVisitor m_Visitor;
        readonly BindingVisitor m_BindingVisitor;

        public TTarget Target
        {
            get => m_Target;
            set => m_Target = value;
        }

        public BindingContextElement Root { get; }
        public InspectorVisitor Visitor { get; }
        public Type DeclaredType { get; }
        public Type TargetType { get; }

        public BindingTarget(BindingContextElement root, TTarget target)
        {
            Root = root;
            m_Target = target;

            DeclaredType = typeof(TTarget);
            TargetType = m_Target.GetType();
            m_Visitor = new InspectorVisitor(Root);
            Visitor = m_Visitor;
            m_BindingVisitor = new BindingVisitor();
        }

        public bool TryGetTarget<T>(out T target)
        {
            if (m_Target is T t)
            {
                target = t;
                return true;
            }

            target = default;
            return false;
        }

        public bool IsPathValid(PropertyPath path)
        {
            return path.Length == 0 || PropertyContainer.IsPathValid(ref m_Target, path);
        }

        public void ReloadAtPath(PropertyPath path, VisualElement current)
        {
            using (var scoped = ScopedVisitor<ReloadAtPathVisitor>.Make())
            {
                var visitor = scoped.Visitor;
                visitor.Path = path;
                visitor.Element = current;
                visitor.Binding = Root;
                PropertyContainer.Accept(visitor, path);
            }
        }

        public void RegisterBindings(PropertyPath path, VisualElement element)
        {
            if (path.IsEmpty)
            {
                BindingUtilities.Bind(element, ref m_Target, path, Root);
                return;
            }

            m_BindingVisitor.Reset();
            m_BindingVisitor.Path = path;
            m_BindingVisitor.Root = Root;
            m_BindingVisitor.Element = element;
            PropertyContainer.Accept(m_BindingVisitor, ref m_Target);
        }

        public void VisitAtPath(IPropertyVisitor visitor, PropertyPath path)
        {
            PropertyContainer.Accept(visitor, ref m_Target, path);
        }

        public void VisitAtPath(PropertyPath path, VisualElement parent)
        {
            VisitAtPath(m_Visitor, path, parent);
        }

        public void VisitAtPath(InspectorVisitor visitor, PropertyPath path, VisualElement parent)
        {
            var contextPath = PropertyPath.Pop(path);
            using (visitor.Context.MakePathOverrideScope(contextPath))
            using (visitor.Context.MakeParentScope(parent))
                PropertyContainer.Accept(visitor, ref m_Target, path);
        }

        public void SetAtPath<TValue>(TValue value, PropertyPath path)
        {
            PropertyContainer.SetValue(ref m_Target, path, value);
        }

        public bool TrySetAtPath<TValue>(TValue value, PropertyPath path)
        {
            return PropertyContainer.TrySetValue(ref m_Target, path, value);
        }

        public TValue GetAtPath<TValue>(PropertyPath path)
        {
            return PropertyContainer.GetValue<TTarget, TValue>(ref m_Target, path);
        }

        public bool TryGetAtPath<TValueType>(PropertyPath path, out TValueType value)
        {
            return PropertyContainer.TryGetValue(ref m_Target, path, out value);
        }

        public bool TryGetProperty(PropertyPath path, out IProperty property)
        {
            return PropertyContainer.TryGetProperty(ref m_Target, path, out property);
        }

        public void GenerateHierarchy()
        {
            Root.Clear();
            using (m_Visitor.Context.MakeParentScope(Root))
            {
                var wrapper = new PropertyWrapper<TTarget>(Target);
                PropertyContainer.Accept(m_Visitor, ref wrapper);
            }
        }

        public void Release()
        {
            Root.Clear();
        }
    }
}
