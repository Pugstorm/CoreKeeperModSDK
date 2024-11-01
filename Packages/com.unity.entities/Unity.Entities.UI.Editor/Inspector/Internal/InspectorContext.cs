using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Context of the inspector that give access to the data.
    /// </summary>
    /// <typeparam name="T">The type of the value being inspected.</typeparam>
    readonly struct InspectorContext<T>
    {
        public readonly BindingContextElement Root;
        public readonly PropertyPath BasePath;
        public readonly PropertyPath PropertyPath;
        public readonly PropertyPathPart Part;

        public readonly string Name;
        public readonly string DisplayName;
        public readonly string Tooltip;

        public readonly bool IsDelayed;
        public readonly bool IsReadOnly;

        public List<Attribute> Attributes { get; }

        public InspectorContext(
            BindingContextElement root,
            PropertyPath propertyPath,
            IProperty property,
            IEnumerable<Attribute> attributes = null
        ){
            Root = root;
            PropertyPath = propertyPath;
            BasePath = PropertyPath;
            if (BasePath.Length > 0)
                BasePath = PropertyPath.Pop(PropertyPath);

            Name = property.Name;
            Part = PropertyPath.Length> 0 ? PropertyPath[PropertyPath.Length - 1] : default;
            var attributeList = new List<Attribute>(attributes ?? property.GetAttributes());
            Attributes = attributeList;
            Tooltip =  property.GetAttribute<TooltipAttribute>()?.tooltip;
            DisplayName = GuiFactory.GetDisplayName(property);
            IsDelayed = property.HasAttribute<DelayedAttribute>();
            IsReadOnly = property.IsReadOnly;
        }

        /// <summary>
        /// Accessor for the data.
        /// </summary>
        public T Data
        {
            get => GetData();
            set => SetData(value);
        }

        T GetData()
        {
            if (PropertyPath.Length == 0)
            {
                return Root.GetTarget<T>();
            }

            if (Root.TryGetValue<T>(PropertyPath, out var value))
            {
                return value;
            }
            throw new InvalidOperationException();
        }

        void SetData(T value)
        {
            if (PropertyPath.Length == 0)
            {
                Root.SetTarget(value);
            }
            else
            {
                Root.SetValue(PropertyPath, value);
            }
            Root.NotifyChanged(PropertyPath);
        }
    }
}
