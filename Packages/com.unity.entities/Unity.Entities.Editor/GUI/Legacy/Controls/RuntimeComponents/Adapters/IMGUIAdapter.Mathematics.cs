using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;
using UnityEditor;

namespace Unity.Editor.Legacy
{
    sealed partial class RuntimeComponentsDrawer :
        IVisitPropertyAdapter<Hash128>
        , IVisitPropertyAdapter<quaternion>
        , IVisitPropertyAdapter<float2>
        , IVisitPropertyAdapter<float3>
        , IVisitPropertyAdapter<float4>
        , IVisitPropertyAdapter<float2x2>
        , IVisitPropertyAdapter<float3x3>
        , IVisitPropertyAdapter<float4x4>
    {
        public void Visit<TContainer>(in VisitContext<TContainer, Hash128> context, ref TContainer container, ref Hash128 value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, quaternion> context, ref TContainer container, ref quaternion value)
        {
            using (MakePathScope(context.Property))
            {
                CustomEditorGUILayout.Vector4Label(GetDisplayName(context.Property), value.value, IsMixedValue("value", value.value));
            }
        }

        public void Visit<TContainer>(in VisitContext<TContainer, float2> context, ref TContainer container, ref float2 value)
        {
            CustomEditorGUILayout.Vector2Label(GetDisplayName(context.Property), value, IsMixedValue(context.Property.Name, value));
        }

        public void Visit<TContainer>(in VisitContext<TContainer, float3> context, ref TContainer container, ref float3 value)
        {
            CustomEditorGUILayout.Vector3Label(GetDisplayName(context.Property), value, IsMixedValue(context.Property.Name, value));
        }

        public void Visit<TContainer>(in VisitContext<TContainer, float4> context, ref TContainer container, ref float4 value)
        {
            CustomEditorGUILayout.Vector4Label(GetDisplayName(context.Property), value, IsMixedValue(context.Property.Name, value));

        }

        public void Visit<TContainer>(in VisitContext<TContainer, float2x2> context, ref TContainer container, ref float2x2 value)
        {
            using (MakePathScope(context.Property))
            {
                CustomEditorGUILayout.Vector2Label(GetDisplayName(context.Property), value.c0, IsMixedValue("c0", value.c0));
                CustomEditorGUILayout.Vector2Label(GetEmptyNameForRow(), value.c1, IsMixedValue("c1", value.c1));
            }
        }

        public void Visit<TContainer>(in VisitContext<TContainer, float3x3> context, ref TContainer container, ref float3x3 value)
        {
            using (MakePathScope(context.Property))
            {
                CustomEditorGUILayout.Vector3Label(GetDisplayName(context.Property), value.c0, IsMixedValue("c0", value.c0));
                CustomEditorGUILayout.Vector3Label(GetEmptyNameForRow(), value.c1, IsMixedValue("c1", value.c1));
                CustomEditorGUILayout.Vector3Label(GetEmptyNameForRow(), value.c2, IsMixedValue("c2", value.c2));
            }
        }

        public void Visit<TContainer>(in VisitContext<TContainer, float4x4> context, ref TContainer container, ref float4x4 value)
        {
            using (MakePathScope(context.Property))
            {
                CustomEditorGUILayout.Vector4Label(GetDisplayName(context.Property), value.c0, IsMixedValue("c0", value.c0));
                CustomEditorGUILayout.Vector4Label(GetEmptyNameForRow(), value.c1, IsMixedValue("c1", value.c1));
                CustomEditorGUILayout.Vector4Label(GetEmptyNameForRow(), value.c2, IsMixedValue("c2", value.c2));
                CustomEditorGUILayout.Vector4Label(GetEmptyNameForRow(), value.c3, IsMixedValue("c3", value.c3));
            }
        }

        static string GetEmptyNameForRow() => EditorGUIUtility.wideMode ? " " : string.Empty;

        bool2 IsMixedValue(string name, float2 value)
        {
            using (MakePathScope(name))
            {
                return new bool2(
                    IsMixedValue("x", value.x),
                    IsMixedValue("y", value.y));
            }
        }

        bool3 IsMixedValue(string name, float3 value)
        {
            using (MakePathScope(name))
            {
                return new bool3(
                    IsMixedValue("x", value.x),
                    IsMixedValue("y", value.y),
                    IsMixedValue("z", value.z));
            }
        }

        bool4 IsMixedValue(string name, float4 value)
        {
            using (MakePathScope(name))
            {
                return new bool4(
                    IsMixedValue("x", value.x),
                    IsMixedValue("y", value.y),
                    IsMixedValue("z", value.z),
                    IsMixedValue("w", value.w));
            }
        }
    }
}
