using Unity.Properties;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    partial class InspectorVisitor
        : IInspectorPrimitiveVisit
        , IInspectorContravariantVisit<UnityEngine.Object>
    {
        delegate TElement FactoryHandler<TValue, out TElement>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            InspectorContext inspectorContext);

        static readonly FactoryHandler<int, IntegerField> IntFieldFactory = GuiFactory.IntField;
        bool IInspectorVisit<int>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref int value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, IntFieldFactory);

        static readonly FactoryHandler<float, FloatField> FloatFieldFactory = GuiFactory.FloatField;
        bool IInspectorVisit<float>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref float value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, FloatFieldFactory);

        static readonly FactoryHandler<sbyte, IntegerField> SByteFieldFactory = GuiFactory.SByteField;
        bool IInspectorVisit<sbyte>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref sbyte value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, SByteFieldFactory);

        static readonly FactoryHandler<short, IntegerField> ShortFieldFactory = GuiFactory.ShortField;
        bool IInspectorVisit<short>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref short value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, ShortFieldFactory);

        static readonly FactoryHandler<long, LongField> LongFieldFactory = GuiFactory.LongField;
        bool IInspectorVisit<long>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref long value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, LongFieldFactory);

        static readonly FactoryHandler<byte, IntegerField> ByteFieldFactory = GuiFactory.ByteField;
        bool IInspectorVisit<byte>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref byte value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, ByteFieldFactory);

        static readonly FactoryHandler<ushort, IntegerField> UShortFieldFactory = GuiFactory.UShortField;
        bool IInspectorVisit<ushort>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref ushort value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, UShortFieldFactory);

        static readonly FactoryHandler<uint, LongField> UIntFieldFactory = GuiFactory.UIntField;
        bool IInspectorVisit<uint>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref uint value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, UIntFieldFactory);

        static readonly FactoryHandler<ulong, TextField> ULongFieldFactory = GuiFactory.ULongField;
        bool IInspectorVisit<ulong>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref ulong value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, ULongFieldFactory);

        static readonly FactoryHandler<double, DoubleField> DoubleFieldFactory = GuiFactory.DoubleField;
        bool IInspectorVisit<double>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref double value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, DoubleFieldFactory);

        static readonly FactoryHandler<bool, Toggle> ToggleFactory = GuiFactory.Toggle;
        bool IInspectorVisit<bool>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref bool value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, ToggleFactory);

        static readonly FactoryHandler<char, TextField> CharFieldFactory = GuiFactory.CharField;
        bool IInspectorVisit<char>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref char value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, CharFieldFactory);

        static readonly FactoryHandler<string, TextField> TextFieldFactory = GuiFactory.TextField;
        bool IInspectorVisit<string>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref string value,
            PropertyPath path)
            => PrimitiveVisit(inspectorContext, property, ref value, path, TextFieldFactory);

        static readonly FactoryHandler<Object, ObjectField> ObjectFieldFactory = GuiFactory.ObjectField;
        bool IInspectorContravariantVisit<UnityEngine.Object>.Visit<TContainer>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            Object value,
            PropertyPath path)
        {
            if (property.HasAttribute<InlineUnityObjectAttribute>() || (inspectorContext.IsRootObject && inspectorContext.Root is InspectorElement))
            {
                return false;
            }
            PrimitiveVisit(inspectorContext, property, ref value, path, ObjectFieldFactory);
            return true;
        }

        bool VisitNull<TContainer, TValue>(InspectorContext inspectorContext, IProperty<TContainer> property, ref TValue value,
            PropertyPath path)
        {
            var element = new NullElement<TValue>(inspectorContext.Root, property, path);
            inspectorContext.Parent.contentContainer.Add(element);
            return true;
        }

        bool VisitEnum<TContainer, TValue>(
            InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref TValue value,
            PropertyPath path,
            bool isWrapper)
        {
            var inspector = GetCustomInspector(property, ref value, path, isWrapper);
            if (null == inspector)
            {
                var underlyingType = System.Enum.GetUnderlyingType(typeof(TValue));
                if (underlyingType == typeof(long))
                {
                    var valueInt64 = System.Convert.ToInt64(value);
                    GuiFactory.LongField(property, ref valueInt64, path, inspectorContext);
                }
                else if (underlyingType == typeof(ulong))
                {
                    var valueUInt64 = System.Convert.ToUInt64(value);
                    GuiFactory.ULongField(property, ref valueUInt64, path, inspectorContext);
                }
                else
                {
                    if (TypeTraits<TValue>.IsEnumFlags)
                        GuiFactory.FlagsField(property, ref value, path, inspectorContext);
                    else
                        GuiFactory.EnumField(property, ref value, path, inspectorContext);
                }
            }
            else
            {
                using (inspectorContext.MakeIgnoreInspectorScope())
                {
                    var customInspector = new CustomInspectorElement(path, inspector, inspectorContext.Root);
                    inspectorContext.Parent.contentContainer.Add(customInspector);
                }
            }

            return true;
        }

        bool PrimitiveVisit<TValue, TElement>(
            InspectorContext inspectorContext,
            IProperty property,
            ref TValue value,
            PropertyPath path,
            FactoryHandler<TValue, TElement> handler)
        {
            var inspector = GetAttributeDrawer(property, ref value, path);
            if (null == inspector)
            {
                handler(property, ref value, path, inspectorContext);
            }
            else
            {
                using (inspectorContext.MakeIgnoreInspectorScope())
                {
                    var customInspector = new CustomInspectorElement(path, inspector, inspectorContext.Root);
                    inspectorContext.Parent.contentContainer.Add(customInspector);
                }
            }

            return true;
        }
    }
}
