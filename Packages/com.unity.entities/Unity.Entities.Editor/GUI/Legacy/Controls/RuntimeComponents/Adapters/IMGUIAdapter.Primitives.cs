using Unity.Properties;

namespace Unity.Editor.Legacy
{
    sealed partial class RuntimeComponentsDrawer : IVisitPrimitivesPropertyAdapter, IVisitPropertyAdapter<string>
    {
        public void Visit<TContainer>(in VisitContext<TContainer, sbyte> context, ref TContainer container, ref sbyte value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, short> context, ref TContainer container, ref short value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, int> context, ref TContainer container, ref int value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, long> context, ref TContainer container, ref long value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, byte> context, ref TContainer container, ref byte value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, ushort> context, ref TContainer container, ref ushort value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, uint> context, ref TContainer container, ref uint value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, ulong> context, ref TContainer container, ref ulong value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, float> context, ref TContainer container, ref float value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, double> context, ref TContainer container, ref double value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, bool> context, ref TContainer container, ref bool value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, char> context, ref TContainer container, ref char value)
        {
            PropertyField(context.Property, value);
        }

        public void Visit<TContainer>(in VisitContext<TContainer, string> context, ref TContainer container, ref string value)
        {
            PropertyField(context.Property, value);
        }
    }
}
