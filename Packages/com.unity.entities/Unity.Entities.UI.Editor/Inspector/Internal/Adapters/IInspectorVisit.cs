using Unity.Properties;

namespace Unity.Entities.UI
{
    interface IInspectorVisit
    {
    }

    interface IInspectorVisit<TValue> : IInspectorVisit
    {
        bool Visit<TContainer>(
            InspectorVisitor.InspectorContext inspectorContext,
            IProperty<TContainer> property,
            ref TValue value,
            PropertyPath path);
    }

    interface IInspectorContravariantVisit<in TValue> : IInspectorVisit
    {
        bool Visit<TContainer>(
            InspectorVisitor.InspectorContext inspectorContext,
            IProperty<TContainer> property,
            TValue value,
            PropertyPath path);
    }

    interface IInspectorPrimitiveVisit
        : IInspectorVisit<sbyte>
            , IInspectorVisit<short>
            , IInspectorVisit<int>
            , IInspectorVisit<long>
            , IInspectorVisit<byte>
            , IInspectorVisit<ushort>
            , IInspectorVisit<uint>
            , IInspectorVisit<ulong>
            , IInspectorVisit<float>
            , IInspectorVisit<double>
            , IInspectorVisit<bool>
            , IInspectorVisit<char>
            , IInspectorVisit<string>
    {
    }
}
