using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    partial class InspectorVisitor
    {
        internal class InspectorContext
        {
            public struct ParentScope : IDisposable
            {
                readonly InspectorContext m_Context;
                readonly VisualElement m_Parent;

                public ParentScope(InspectorContext inspectorContext, VisualElement parent)
                {
                    m_Context = inspectorContext;
                    m_Parent = parent;
                    m_Context.PushParent(m_Parent);
                }

                public void Dispose()
                {
                    m_Context.PopParent(m_Parent);
                }
            }

            public readonly struct IgnoreInspectorScope : IDisposable
            {
                readonly InspectorContext m_Context;

                public IgnoreInspectorScope(InspectorContext inspectorContext)
                {
                    m_Context = inspectorContext;
                    ++m_Context.m_IgnoreNextInspectorsCount;
                }

                public void Dispose()
                {
                    --m_Context.m_IgnoreNextInspectorsCount;
                }
            }

            public readonly struct PathScope : IDisposable
            {
                readonly InspectorContext m_Context;
                readonly IProperty m_Property;

                public PathScope(InspectorContext context, IProperty property)
                {
                    m_Context = context;
                    m_Property = property;
                    m_Context.AddToPath(m_Property);
                }

                public void Dispose()
                {
                    m_Context.RemoveFromPath(m_Property);
                }
            }

            public readonly struct PathOverrideScope : IDisposable
            {
                readonly InspectorContext m_Context;
                readonly PropertyPath m_Path;

                public PathOverrideScope(InspectorContext context, PropertyPath path)
                {
                    m_Context = context;
                    m_Path = context.CopyCurrentPath();
                    m_Context.ClearPath();
                    m_Context.AddToPath(path);
                }

                public void Dispose()
                {
                    m_Context.ClearPath();
                    m_Context.AddToPath(m_Path);
                }
            }

            public struct VisitedReferencesScope<TValue> : IDisposable
            {
                readonly InspectorContext _mInspectorContext;
                readonly object m_Object;
                readonly bool m_ReferenceType;
                public readonly bool VisitedOnCurrentBranch;

                public PropertyPath GetReferencePath()
                {
                    return _mInspectorContext.m_References.GetPath(m_Object);
                }

                public VisitedReferencesScope(InspectorContext inspectorContext, ref TValue value, PropertyPath path)
                {
                    _mInspectorContext = inspectorContext;
                    m_ReferenceType = !TypeTraits<TValue>.IsValueType;

                    if (m_ReferenceType)
                    {
                        if (EqualityComparer<TValue>.Default.Equals(value, default))
                        {
                            m_Object = null;
                            VisitedOnCurrentBranch = false;
                            return;
                        }

                        m_ReferenceType = !value.GetType().IsValueType;
                    }

                    if (m_ReferenceType)
                    {
                        m_Object = value;
                        VisitedOnCurrentBranch = !_mInspectorContext.PushReference(value, path);
                    }
                    else
                    {
                        m_Object = null;
                        VisitedOnCurrentBranch = false;
                    }
                }

                public void Dispose()
                {
                    if (m_ReferenceType)
                    {
                        _mInspectorContext.PopReference(m_Object);
                    }
                }
            }

            public readonly BindingContextElement Root;
            public bool IsRootObject;
            readonly Stack<VisualElement> m_ParentStack;
            readonly InspectedReferences m_References;
            PropertyPath m_Path = new PropertyPath();

            int m_IgnoreNextInspectorsCount;

            public VisualElement Parent
            {
                get
                {
                    if (m_ParentStack.Count > 0)
                    {
                        return m_ParentStack.Peek();
                    }
                    throw new InvalidOperationException($"A parent element must be set.");
                }
            }

            public InspectorContext(BindingContextElement root)
            {
                Root = root;
                m_ParentStack = new Stack<VisualElement>();
                m_References = new InspectedReferences();
            }

            public bool NextInspectorIsIgnored()
            {
                var visit = m_IgnoreNextInspectorsCount > 0;
                --m_IgnoreNextInspectorsCount;
                return visit;
            }

            public void SkipNextInspector(int count)
            {
                m_IgnoreNextInspectorsCount += count;
            }

            public PathOverrideScope MakePathOverrideScope(PropertyPath path)
            {
                return new PathOverrideScope(this, path);
            }

            public PathScope MakePathScope(IProperty property)
            {
                return new PathScope(this, property);
            }

            public IgnoreInspectorScope MakeIgnoreInspectorScope()
            {
                return new IgnoreInspectorScope(this);
            }

            public ParentScope MakeParentScope(VisualElement parent)
            {
                return new ParentScope(this, parent);
            }

            void PushParent(VisualElement parent)
            {
                m_ParentStack.Push(parent);
            }

            void PopParent(VisualElement parent)
            {
                if (m_ParentStack.Peek() == parent)
                {
                    m_ParentStack.Pop();
                }
                else
                {
                    Debug.LogError($"{nameof(InspectorContext)}.{nameof(MakeParentScope)} was not properly disposed for parent: {parent?.name}");
                }
            }

            public VisitedReferencesScope<TValue> MakeVisitedReferencesScope<TValue>(ref TValue value, PropertyPath path)
            {
                return new VisitedReferencesScope<TValue>(this, ref value, path);
            }

            bool PushReference(object obj, PropertyPath path)
                => m_References.PushReference(obj, path);

            void PopReference(object obj)
                => m_References.PopReference(obj);

            public void Reset()
            {
                m_Path = default;
                m_References.Clear();
                m_ParentStack.Clear();
                m_IgnoreNextInspectorsCount = 0;
            }

            public PropertyPath CopyCurrentPath()
            {
                return m_Path;
            }

            public bool IsPathEmpty()
            {
                return m_Path.IsEmpty;
            }

            public void AddToPath(IProperty property)
            {
                if (property is IListElementProperty listElementProperty)
                {
                    PushPathPart(new PropertyPathPart(listElementProperty.Index));
                }
                else if (property is IDictionaryElementProperty dictionaryElementProperty)
                {
                    PushPathPart(new PropertyPathPart((object)dictionaryElementProperty.ObjectKey));
                }
                else
                {
                    PushPathPart(new PropertyPathPart(property.Name));
                }
            }

            public void RemoveFromPath(IProperty property)
            {
                PopPathPart();
            }

            public void AddToPath(PropertyPath path)
            {
                m_Path = PropertyPath.Combine(m_Path, path);
            }

            public void RemoveFromPath(PropertyPath path)
            {
                m_Path = PropertyPath.SubPath(m_Path, 0, m_Path.Length - path.Length);
            }

            void ClearPath()
            {
                m_Path = default;
            }

            void PushPathPart(PropertyPathPart path)
            {
                m_Path = PropertyPath.AppendPart(m_Path, path);
            }

            void PopPathPart()
            {
                m_Path = PropertyPath.Pop(m_Path);
            }
        }
    }
}
