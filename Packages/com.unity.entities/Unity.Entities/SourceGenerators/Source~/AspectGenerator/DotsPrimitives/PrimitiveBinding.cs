namespace Unity.Entities.SourceGen.Aspect
{
    /// <summary>
    /// A PrimitiveBinding specify how a primitive such as ComponentLookup or BufferTypeHandle access it's data.
    /// it is composed of the component type argument, a read-write access and an optional flag.
    /// </summary>
    public readonly struct PrimitiveBinding : IPrintable
    {
        /// <summary>
        /// The component Type. Usually defined by the generic type arguments : "ComponentTypeHandle<ComponentType>"
        /// Default to first component type name when merging.
        /// </summary>
        public readonly string ComponentTypeName;

        /// <summary>
        /// True: the data will only be read from.
        /// False: the data may be written to.
        /// an read-write access will override a read-only one when merging 2 PrimitiveBinding
        /// </summary>
        public readonly bool IsReadOnly;

        /// <summary>
        /// The data may not be present on every entities requested by a query
        /// an non-optional binding will override an optional one when merging 2 PrimitiveBinding
        /// </summary>
        public readonly bool IsOptional;

        public PrimitiveBinding(string componentTypename, bool isReadOnly, bool isOptional)
        {
            ComponentTypeName = componentTypename;
            IsReadOnly = isReadOnly;
            IsOptional = isOptional;
        }

        /// <summary>
        /// Merge 2 binding together.
        /// The component type name must be the same.
        /// The readonly and optional flag are recessive.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static PrimitiveBinding Merge(PrimitiveBinding a, PrimitiveBinding b)
        {
            System.Diagnostics.Debug.Assert(a.ComponentTypeName == b.ComponentTypeName);
            return new PrimitiveBinding(a.ComponentTypeName, a.IsReadOnly && b.IsReadOnly,
                a.IsOptional && b.IsOptional);
        }

        /// <summary>
        /// return b overriding a
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static PrimitiveBinding Override(PrimitiveBinding a, PrimitiveBinding b)
        {
            return new PrimitiveBinding(b.ComponentTypeName ?? a.ComponentTypeName, a.IsReadOnly || b.IsReadOnly,
                a.IsOptional || b.IsOptional);
        }

        public Printer PrintQueryComponentType(Printer printer, bool forceReadOnly)
            => printer.Print("global::Unity.Entities.ComponentType.")
                .Print(IsReadOnly | forceReadOnly ? "ReadOnly" : "ReadWrite")
                .Print("<")
                .Print(ComponentTypeName)
                .Print(">()");

        /// <summary>
        /// Print a human readable state of this PrimitiveBinding
        /// </summary>
        /// <param name="printer"></param>
        public void Print(Printer printer)
        {
            printer.Print("PrimitiveBinding(")
                .Print(IsReadOnly ? "RO, " : "RW, ")
                .Print(ComponentTypeName);
            if (IsOptional)
                printer.Print(", Optional");
            printer.Print(")");
        }
    }
}
