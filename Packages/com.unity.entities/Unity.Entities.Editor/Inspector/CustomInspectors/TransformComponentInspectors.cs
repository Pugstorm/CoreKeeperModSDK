using JetBrains.Annotations;
using Unity.Mathematics;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Inspectors
{
    abstract class Float4x4ValueInspector<T> : PropertyInspector<T>
    {
        static Float4x4ValueInspector()
        {
            TypeConversion.Register((ref float4 v) => (Vector4) v);
            TypeConversion.Register((ref Vector4 v) => (float4) v);
        }

        public override VisualElement Build()
        {
            var root = new BindableElement
            {
                bindingPath = "Value"
            };

            for (var i = 0; i < 4; ++i)
            {
                var column = new Vector4Field { bindingPath = "c" + i };
                column.Query<FloatField>().ForEach(field => field.formatString = "0.###");
                InspectorUtility.AddRuntimeBar(column);
                root.Add(column);
            }

            return root;
        }
    }

    [UsedImplicitly]
    sealed class LocalToWorldInspector : Float4x4ValueInspector<LocalToWorld>
    {
    }

    abstract class Float3ValueInspector<T> : PropertyInspector<T>
    {
        static Float3ValueInspector()
        {
            TypeConversion.Register((ref float3 v) => (Vector3) v);
            TypeConversion.Register((ref Vector3 v) => (float3) v);
        }

        public override VisualElement Build()
        {
            var valueField = new Vector3Field { bindingPath = "Value" };
            valueField.Query<FloatField>().ForEach(field => field.formatString = "0.###");
            return valueField;
        }
    }

    abstract class QuaternionValueInspector<T> : PropertyInspector<T>
    {
        static QuaternionValueInspector()
        {
            TypeConversion.Register((ref quaternion v) => (Vector4) v.value);
            TypeConversion.Register((ref Vector4 v) => new quaternion { value = v });
        }

        public override VisualElement Build()
        {
            var valueField = new Vector4Field { bindingPath = "Value" };
            valueField.Query<FloatField>().ForEach(field => field.formatString = "0.###");
            return valueField;
        }
    }

    abstract class DefaultValueInspector<T> : PropertyInspector<T>
    {
        public override VisualElement Build()
        {
            var root = new VisualElement();
            DoDefaultGui(root, "Value");
            return root;
        }
    }

    [UsedImplicitly]
    class ParentInspector : DefaultValueInspector<Parent>
    {
    }

    [UsedImplicitly]
    class PreviousParentInspector : DefaultValueInspector<PreviousParent>
    {
    }

    [UsedImplicitly]
    class ChildInspector : DefaultValueInspector<Child>
    {
    }
}
