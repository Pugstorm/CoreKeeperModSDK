using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Unity.Entities.UI
{
    static class GuiFactory
    {
        public static NullableFoldout<TValue> Foldout<TValue>(
            IProperty property,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => ConstructFoldout<NullableFoldout<TValue>>(property, path, visitorInspectorContext);

        public static IListElement<TList, TElement> Foldout<TList, TElement>(
            IProperty property,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            where TList : IList<TElement>
            => ConstructFoldout<IListElement<TList, TElement>>(property, path, visitorInspectorContext);

        public static DictionaryElement<TDictionary, TKey, TValue> Foldout<TDictionary, TKey, TValue>(
            IProperty property,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            where TDictionary : IDictionary<TKey, TValue>
            => ConstructFoldout<DictionaryElement<TDictionary, TKey, TValue>>(
                property, path, visitorInspectorContext);

        public static HashSetElement<TSet, TElement> SetFoldout<TSet, TElement>(
            IProperty property,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            where TSet : ISet<TElement>
            => ConstructFoldout<HashSetElement<TSet, TElement>>(property, path, visitorInspectorContext);

        public static Toggle Toggle(
            IProperty property,
            ref bool value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<Toggle, bool>(property, ref value, path, visitorInspectorContext);

        public static IntegerField SByteField(
            IProperty property,
            ref sbyte value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => ConstructWithConverter<IntegerField, int, sbyte>(property, ref value, path, visitorInspectorContext, s_IntToSByte);

        static readonly Func<int, sbyte> s_IntToSByte = IntToSByte;
        static sbyte IntToSByte(int arg)
        {
            return (sbyte)Mathf.Clamp(arg, sbyte.MinValue, sbyte.MaxValue);
        }

        public static IntegerField ByteField(
            IProperty property,
            ref byte value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => ConstructWithConverter<IntegerField, int, byte>(property, ref value, path, visitorInspectorContext, s_IntToByte);

        static readonly Func<int, byte> s_IntToByte = IntToByte;
        static byte IntToByte(int arg)
        {
            return (byte)Mathf.Clamp(arg, byte.MinValue, byte.MaxValue);
        }

        public static IntegerField UShortField(
            IProperty property,
            ref ushort value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => ConstructWithConverter<IntegerField, int, ushort>(property, ref value, path, visitorInspectorContext, IntToUShort);

        static readonly Func<int, ushort> s_IntToUShort = IntToUShort;
        static ushort IntToUShort(int arg)
        {
            return (ushort)Mathf.Clamp(arg, ushort.MinValue, ushort.MaxValue);
        }

        public static IntegerField ShortField(
            IProperty property,
            ref short value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => ConstructWithConverter<IntegerField, int, short>(property, ref value, path, visitorInspectorContext, s_IntToShort);

        static readonly Func<int, short> s_IntToShort = IntToShort;
        static short IntToShort(int arg)
        {
            return (short)Mathf.Clamp(arg, short.MinValue, short.MaxValue);
        }

        public static IntegerField IntField(
            IProperty property,
            ref int value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<IntegerField, int>(property, ref value, path, visitorInspectorContext);

        public static LongField UIntField(
            IProperty property,
            ref uint value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => ConstructWithConverter<LongField, long, uint>(property, ref value, path, visitorInspectorContext, s_LongToUInt);

        static readonly Func<long, uint> s_LongToUInt = LongToUInt;
        static uint LongToUInt(long arg)
        {
            return (uint)Mathf.Clamp(arg, uint.MinValue, uint.MaxValue);
        }

        public static LongField LongField(
            IProperty property,
            ref long value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<LongField, long>(property, ref value, path, visitorInspectorContext);

        public static TextField ULongField(
            IProperty property,
            ref ulong value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<TextField, string, ulong>(property, ref value, path,
                visitorInspectorContext);

        public static FloatField FloatField(
            IProperty property,
            ref float value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<FloatField, float>(property, ref value, path, visitorInspectorContext);

        public static DoubleField DoubleField(
            IProperty property,
            ref double value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<DoubleField, double>(property, ref value, path, visitorInspectorContext);

        public static TextField CharField(
            IProperty property,
            ref char value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<TextField, string, char>(property, ref value, path, visitorInspectorContext, (field, prop) => field.maxLength = 1);

        public static TextField TextField(
            IProperty property,
            ref string value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
            => Construct<TextField, string, string>(property, ref value, path,
                visitorInspectorContext);

        public static ObjectField ObjectField(
            IProperty property,
            ref UnityEngine.Object value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
        {
            var element = Construct<ObjectField, UnityEngine.Object, UnityEngine.Object>(
                property,
                ref value,
                path,
                visitorInspectorContext,
                (field, p) => field.objectType = p.DeclaredValueType());
            return element;
        }

        public static EnumFlagsField FlagsField<TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
        {
            if (!typeof(TValue).IsEnum)
            {
                throw new ArgumentException();
            }

            var element = Construct<EnumFlagsField, Enum, TValue>(
                property,
                ref value,
                path,
                visitorInspectorContext);
            element.Init(value as Enum);
            return element;
        }

        public static EnumField EnumField<TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext)
        {
            if (!typeof(TValue).IsEnum)
            {
                throw new ArgumentException();
            }

            var element = Construct<EnumField, Enum, TValue>(
                property,
                ref value,
                path,
                visitorInspectorContext);
            element.Init(value as Enum);
            return element;
        }


        static TElement ConstructBase<TElement, TFieldValue>(IProperty property, VisualElement parent)
            where TElement : BaseField<TFieldValue>, new()
        {
            var element = new TElement();
            SetNames(property, element);
            SetTooltip(property, element);
            SetDelayed(property, element);
            SetReadOnly(property, element);
            parent.contentContainer.Add(element);
            element.RegisterCallback<AttachToPanelEvent, VisualElement>(AddRuntimeBar, parent);
            return element;
        }

        /// <summary>
        /// Add runtime bar when parent element is the root PropertyElement with user data or
        /// its ancestor is the root PropertyElement with user data.
        /// User data is set to the root PropertyElement to indicate this is a live property displaying runtime data,
        /// hence we add the runtime bar to the field.
        /// </summary>
        static void AddRuntimeBar(AttachToPanelEvent evt, VisualElement parent)
        {
            if (evt.target is not VisualElement element || element.ClassListContains(UssClasses.RuntimeBarElement.RuntimeBarAdded))
                return;

            var isParentLiveProperty = parent is PropertyElement {userData: { }};
            var isAncestorLiveProperty = parent?.GetFirstAncestorOfType<PropertyElement>()?.userData;

            if (isParentLiveProperty || isAncestorLiveProperty != null)
            {
                var runtimeBar = new VisualElement();
                runtimeBar.AddToClassList(UssClasses.RuntimeBarElement.RuntimeBar);
                element.Add(runtimeBar);
                element.AddToClassList(UssClasses.RuntimeBarElement.RuntimeBarAdded);
                element.RegisterCallback<GeometryChangedEvent, VisualElement>(SetRuntimeBarPosition, runtimeBar);
            }

            element.UnregisterCallback<AttachToPanelEvent, VisualElement>(AddRuntimeBar);
        }

        static void SetRuntimeBarPosition(GeometryChangedEvent evt, VisualElement runtimeBar)
        {
            if (evt.target is not VisualElement element)
                return;

            runtimeBar.style.left = 2f - element.worldBound.x;
            element.UnregisterCallback<GeometryChangedEvent, VisualElement>(SetRuntimeBarPosition);
        }

        static TElement ConstructBase<TElement>(IProperty property, VisualElement parent)
            where TElement : Foldout, new()
        {
            var element = new TElement();
            SetNames(property, element);
            SetTooltip(property, element);
            SetReadOnly(property, element);
            parent.contentContainer.Add(element);
            return element;
        }

        static void SetNames<TValue>(IProperty property, BaseField<TValue> element)
        {
            SetCommonNames(property, element);
            element.label = GetDisplayName(property);
        }

        static void SetNames(IProperty property, Foldout element)
        {
            SetCommonNames(property, element);
            element.text = GetDisplayName(property);
        }

        static void SetCommonNames(IProperty property, BindableElement element)
        {
            var name = property.Name;
            element.name = name;
            element.bindingPath = name;
            element.AddToClassList(name);
        }

        internal static string GetDisplayName(IProperty property)
        {
            var name = property.Name;
            return property is ICollectionElementProperty
                ? $"Element {name}"
                : property.HasAttribute<DisplayNameAttribute>()
                    ? property.GetAttribute<DisplayNameAttribute>().Name
                    : property.HasAttribute<InspectorNameAttribute>()
                        ? property.GetAttribute<InspectorNameAttribute>().displayName
                        : ObjectNames.NicifyVariableName(name);
        }

        internal static void SetTooltip(IProperty property, VisualElement element)
        {
            if (property.HasAttribute<TooltipAttribute>())
            {
                element.tooltip = property.GetAttribute<TooltipAttribute>().tooltip;
            }
        }

        static void SetDelayed<TFieldValue>(IProperty property, BaseField<TFieldValue> element)
        {
            if (property.HasAttribute<DelayedAttribute>() && element is TextInputBaseField<TFieldValue> textInput)
            {
                textInput.isDelayed = true;
            }
        }

        static void SetReadOnly(IProperty property, VisualElement element)
        {
            if (property.IsReadOnly && (property.DeclaredValueType().IsValueType || !TypeTraits.IsContainer(property.DeclaredValueType())))
            {
                element.SetEnabled(false);
            }
        }

        static TElement Construct<TElement, TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext
        )
            where TElement : BaseField<TValue>, new()
        {
            return Construct<TElement, TValue, TValue>(property, ref value, path,
                visitorInspectorContext);
        }

        static TElement ConstructFoldout<TElement>(
            IProperty property,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext
        )
            where TElement : Foldout, IContextElement, new()
        {
            var element = ConstructBase<TElement>(property, visitorInspectorContext.Parent);
            element.SetContext(visitorInspectorContext.Root, path);
            var targetType = visitorInspectorContext.Root.GetTargetType();
            element.SetValueWithoutNotify(UiPersistentState.GetFoldoutState(targetType, path));
            element.RegisterCallback<ChangeEvent<bool>>(evt => UiPersistentState.SetFoldoutState(targetType, path, evt.newValue));
            return element;
        }

        abstract class UIBinding<TElement, TValue> : IBinding
            where TElement : VisualElement
        {
            protected BindingContextElement Root;
            protected PropertyPath Path;
            protected TElement Element;

            protected UIBinding(TElement element, BindingContextElement root, PropertyPath path)
            {
                Element = element;
                Root = root;
                Path = path;
            }

            public void PreUpdate()
            {
            }

            public abstract void Update();

            public void Release()
            {
            }
        }

        class TextureBinding<TValue> : UIBinding<BindableElement, TValue>
        {
            public TextureBinding(BindableElement element, BindingContextElement root, PropertyPath path) : base(element, root, path)
            {
            }

            public override void Update()
            {
                if (!Root.TryGetValue<TValue>(Path, out var value))
                    return;

                if (!TypeConversion.TryConvert(ref value, out Texture2D texture))
                    return;

                Element.style.backgroundImage = texture;
            }
        }

        class LabelBinding<TValue> : UIBinding<Label, TValue>
        {
            public LabelBinding(Label element, BindingContextElement root, PropertyPath path) : base(element, root, path)
            {
            }

            public override void Update()
            {
                if (!Root.TryGetValue<TValue>(Path, out var value))
                    return;

                Element.text = TypeConversion.TryConvert(ref value, out string strValue) ? strValue : value.ToString();
            }
        }

        class PropertyBinding<TValue> : UIBinding<BindingContextElement, TValue>
        {
            public PropertyBinding(BindingContextElement element, BindingContextElement root, PropertyPath path) : base(element, root, path)
            {
            }

            public override void Update()
            {
                if (!Root.TryGetValue<TValue>(Path, out var value))
                    return;

                Element.SetTarget(value);
            }
        }

        class Binding<TFieldType, TValue> : UIBinding<BaseField<TFieldType>, TValue>
        {
            public Binding(BaseField<TFieldType> element, BindingContextElement root, PropertyPath path) : base(element, root, path)
            {
            }

            public override void Update()
            {
                if (!Root.TryGetValue<TValue>(Path, out var value))
                    return;

                if (!TypeConversion.TryConvert(ref value, out TFieldType fieldValue))
                    return;

                if (EqualityComparer<TFieldType>.Default.Equals(fieldValue, Element.value))
                    return;

                if (Element?.focusController?.focusedElement != Element)
                {
                    Element.SetValueWithoutNotify(fieldValue);
                }
            }
        }

        static TElement Construct<TElement, TFieldType, TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext,
            Action<TElement, IProperty> initializer = null
        )
            where TElement : BaseField<TFieldType>, new()
        {
            var element = ConstructBase<TElement, TFieldType>(property, visitorInspectorContext.Parent);
            initializer?.Invoke(element, property);

            SetCallbacks(ref value, path, visitorInspectorContext.Root, element);
            return element;
        }

        static TElement ConstructWithConverter<TElement, TFieldType, TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            InspectorVisitor.InspectorContext visitorInspectorContext,
            Func<TFieldType, TValue> converter,
            Action<TElement, IProperty> initializer = null
        )
            where TElement : BaseField<TFieldType>, new()
        {
            var element = ConstructBase<TElement, TFieldType>(property, visitorInspectorContext.Parent);
            initializer?.Invoke(element, property);

            SetCallbacks(ref value, path, visitorInspectorContext.Root, element, converter);
            return element;
        }

        internal static void SetCallbacks<TValue>(
            ref TValue value,
            PropertyPath path,
            BindingContextElement root,
            BindingContextElement field)
        {
            field.SetRoot(root);
            field.SetTarget(value);
            field.binding = new PropertyBinding<TValue>(field, root, path);

            var r = root;
            var p = path;
            field.OnChanged += (element, propertyPath) =>
            {
                r.SetValue(p, element.GetTarget<TValue>());
                element.SetTarget(r.GetValue<TValue>(p));
                var fullPath = PropertyPath.Combine(p, propertyPath);
                r.NotifyChanged(fullPath);
            };
        }

        internal static void SetCallbacks<TFieldType, TValue>(
            ref TValue value,
            PropertyPath path,
            BindingContextElement root,
            BaseField<TFieldType> field)
        {
            if (TypeConversion.TryConvert(ref value, out TFieldType fieldValue))
            {
                field.SetValueWithoutNotify(fieldValue);
                field.binding = new Binding<TFieldType, TValue>(field, root, path);
            }

            field.RegisterCallback<ChangeEvent<TFieldType>, ValueChangedContext<TFieldType, TValue>>(ValueChanged, new ValueChangedContext<TFieldType, TValue>
            {
                path = path,
                converter = null
            });
        }

        internal static void SetCallbacks<TFieldType, TValue>(
            ref TValue value,
            PropertyPath path,
            BindingContextElement root,
            BaseField<TFieldType> field,
            Func<TFieldType, TValue> converter)
        {
            if (TypeConversion.TryConvert(ref value, out TFieldType fieldValue))
            {
                field.SetValueWithoutNotify(fieldValue);
                field.binding = new Binding<TFieldType, TValue>(field, root, path);
            }

            field.RegisterCallback<ChangeEvent<TFieldType>, ValueChangedContext<TFieldType, TValue>>(ValueChanged, new ValueChangedContext<TFieldType, TValue>
            {
                path = path,
                converter = converter
            });
        }

        internal static void SetCallbacks<TValue>(
            ref TValue value,
            PropertyPath path,
            BindingContextElement root,
            BindableElement element)
        {
            if (!TypeConversion.TryConvert(ref value, out Texture2D texture))
                return;

            element.style.backgroundImage = texture;
            element.binding = new TextureBinding<TValue>(element, root, path);
        }

        internal static void SetCallbacks<TValue>(
            ref TValue value,
            PropertyPath path,
            BindingContextElement root,
            Label label)
        {
            label.text = TypeConversion.TryConvert(ref value, out string strValue) ? strValue : value.ToString();
            label.binding = new LabelBinding<TValue>(label, root, path);
        }

        struct ValueChangedContext<TFieldType, TValue>
        {
            public PropertyPath path;
            public Func<TFieldType, TValue> converter;
        }

        static void ValueChanged<TFieldType, TValue>(ChangeEvent<TFieldType> evt, ValueChangedContext<TFieldType, TValue> context)
        {
            var field = evt.target as BaseField<TFieldType>;
            var element = field?.GetFirstAncestorOfType<BindingContextElement>();
            if (null == element)
                return;

            TValue newValue;
            var fieldNewValue = evt.newValue;
            if (null != context.converter)
                newValue = context.converter(fieldNewValue);
            else if (!TypeConversion.TryConvert(ref fieldNewValue, out newValue))
                return;

            if (!element.TryGetValue(context.path, out TValue oldValue))
            {
                var fieldOldValue = evt.previousValue;
                if (null != context.converter)
                    oldValue = context.converter(fieldOldValue);
                else if (!TypeConversion.TryConvert(ref fieldOldValue, out oldValue))
                    return;
            }

            if (!element.TrySetValue(context.path, newValue))
                return;

            if (!element.TryGetValue(context.path, out TValue newValueAfterSet))
            {
                newValueAfterSet = newValue;
            }

            if (TypeConversion.TryConvert(ref newValueAfterSet, out TFieldType newFieldValue))
            {
                field.SetValueWithoutNotify(newFieldValue);
            }

            if (!TypeTraits<TValue>.IsValueType && EqualityComparer<TValue>.Default.Equals(newValueAfterSet, default))
            {
                if (!EqualityComparer<TValue>.Default.Equals(oldValue, default))
                {
                    element.NotifyChanged(context.path);
                }
            }
            else
            {
                if (!EqualityComparer<TValue>.Default.Equals(newValueAfterSet, oldValue))
                    element.NotifyChanged(context.path);
            }
        }
    }
}
