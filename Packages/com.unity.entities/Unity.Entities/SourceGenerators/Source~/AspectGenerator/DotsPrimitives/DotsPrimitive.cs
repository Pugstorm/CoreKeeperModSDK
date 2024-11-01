using System;

namespace Unity.Entities.SourceGen.Aspect
{
    public enum PrimitiveType
    {
        Nothing,
        BufferAccessor,
        ComponentNativeArray,
        EnabledMask,
        EntityNativeArray,
        Entity,
        BufferTypeHandle,
        ComponentTypeHandle,
        EntityTypeHandle,
        SharedComponentTypeHandle,
        BufferLookup,
        ComponentLookup,
        EntityLookup,
        EntityStorageInfoLookup,
        SharedComponentLookup,  // emulated using a SharedComponentTypeHandle and a single StorageInfoLookup for all SharedComponentTypeHandle
        DynamicBuffer,
        EnabledRef,
        Ref,
        SharedComponent
    }

    public struct DotsPrimitive : IPrintable
    {
        public PrimitiveType Type;
        public DotsPrimitive(PrimitiveType type)
        {
            Type = type;
        }

        public bool IsNothing => Type == PrimitiveType.Nothing;


        /// <summary>
        /// A dots primitive tag is used to build unique names for each primitive type
        /// </summary>
        public string Tag
        {
            get
            {
                switch (Type)
                {
                    case PrimitiveType.Nothing:
                        return "";
                    case PrimitiveType.BufferAccessor:
                        return "Ba";
                    case PrimitiveType.ComponentNativeArray:
                        return "NaC";
                    case PrimitiveType.EnabledMask:
                        return "Enm";
                    case PrimitiveType.EntityNativeArray:
                        return "NaE";
                    case PrimitiveType.Entity:
                        return "E";
                    case PrimitiveType.BufferTypeHandle:
                        return "Bth";
                    case PrimitiveType.ComponentTypeHandle:
                        return "Cth";
                    case PrimitiveType.EntityTypeHandle:
                        return "Eth";
                    case PrimitiveType.SharedComponentTypeHandle:
                        return "Scth";
                    case PrimitiveType.BufferLookup:
                        return "Bl";
                    case PrimitiveType.ComponentLookup:
                        return "C" + "l";
                    case PrimitiveType.EntityLookup:
                        return "El";
                    case PrimitiveType.EntityStorageInfoLookup:
                        return "Esil";
                    case PrimitiveType.SharedComponentLookup:
                        return "Scl";
                    case PrimitiveType.DynamicBuffer:
                        return "Db";
                    case PrimitiveType.EnabledRef:
                        return "Enref";
                    case PrimitiveType.Ref:
                        return "Ref";
                    case PrimitiveType.SharedComponent:
                        return "Sc";
                }
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Print the type name of the primitive using a specific binding
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="Bind"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer TypeName(Printer printer, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                    return printer;
                case PrimitiveType.BufferAccessor:
                    return printer.Print($"global::Unity.Entities.BufferAccessor<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.ComponentNativeArray:
                    return printer.Print("global::Unity.Collections.NativeArray<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.EnabledMask:
                    return printer.Print("global::Unity.Entities.EnabledMask");
                case PrimitiveType.EntityNativeArray:
                    return printer.Print("global::Unity.Collections.NativeArray<global::Unity.Entities.Entity>");
                case PrimitiveType.Entity:
                    return printer.Print("global::Unity.Entities.Entity");
                case PrimitiveType.BufferTypeHandle:
                    return printer.Print("global::Unity.Entities.BufferTypeHandle<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.ComponentTypeHandle:
                    return printer.Print("global::Unity.Entities.ComponentTypeHandle<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.EntityTypeHandle:
                    return printer.Print("global::Unity.Entities.EntityTypeHandle");
                case PrimitiveType.SharedComponentTypeHandle:
                    return printer.Print("global::Unity.Entities.SharedComponentTypeHandle<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.BufferLookup:
                    return printer.Print($"global::Unity.Entities.BufferLookup<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.ComponentLookup:
                    return printer.Print("global::Unity.Entities.ComponentLookup<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.EntityLookup:
                    return printer;
                case PrimitiveType.EntityStorageInfoLookup:
                    return printer.Print("global::Unity.Entities.EntityStorageInfoLookup");
                case PrimitiveType.SharedComponentLookup: // will use a EntityStorageInfoLookup to get the chunk and then use the Scth to decay to the SharedComponent.
                    return printer.Print("global::Unity.Entities.SharedComponentTypeHandle<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.DynamicBuffer:
                    return printer.Print("global::Unity.Entities.DynamicBuffer<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.EnabledRef:
                    return printer.Print("global::Unity.Entities.EnabledRef").Print(Bind.IsReadOnly ? "RO" : "RW").Print("<").Print(Bind.ComponentTypeName).Print(">");
                case PrimitiveType.Ref:
                    return RefTypeName(printer, Bind.IsReadOnly, Bind.ComponentTypeName);
                case PrimitiveType.SharedComponent:
                    return printer.PrintBeginLine(Bind.ComponentTypeName);
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Print the type name of a Ref primitive
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="isReadOnly"></param>
        /// <param name="componentName"></param>
        /// <returns></returns>
        public static Printer RefTypeName(Printer printer, bool isReadOnly, string componentName) => printer.Print("global::Unity.Entities.Ref").Print(isReadOnly ? "RO" : "RW").Print("<").Print(componentName).Print(">");

        /// <summary>
        /// Declare a field of the primitive type using a specific binding
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="fieldName">The name for the primitive field declaration</param>
        /// <param name="Bind">The binding used for the primitive</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer Declare(Printer printer, string fieldName, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                    return printer;
                case PrimitiveType.BufferAccessor:
                    return printer.PrintBeginLine($"public global::Unity.Entities.BufferAccessor<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.ComponentNativeArray:
                    return printer.PrintBeginLine("public global::Unity.Collections.NativeArray<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.EnabledMask:
                    return printer.PrintBeginLine("public global::Unity.Entities.EnabledMask ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.EntityNativeArray:
                    return printer.PrintBeginLine("public global::Unity.Collections.NativeArray<global::Unity.Entities.Entity> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.Entity:
                    return printer.PrintBeginLine("global::Unity.Entities.Entity ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.BufferTypeHandle:
                    return printer.PrintBeginLine("global::Unity.Entities.BufferTypeHandle<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.ComponentTypeHandle:
                    return printer.PrintLineIf(Bind.IsReadOnly, "[global::Unity.Collections.ReadOnly]")
                        .PrintBeginLine("global::Unity.Entities.ComponentTypeHandle<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.EntityTypeHandle:
                    return printer.PrintBeginLine("global::Unity.Entities.EntityTypeHandle ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.SharedComponentTypeHandle:
                    return printer.PrintBeginLine($"public global::Unity.Entities.SharedComponentTypeHandle<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.BufferLookup:
                    return printer.PrintBeginLine($"global::Unity.Entities.BufferLookup<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.ComponentLookup:
                    return printer.PrintLineIf(Bind.IsReadOnly, "[global::Unity.Collections.ReadOnly]")
                        .PrintBeginLine("global::Unity.Entities.ComponentLookup<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.EntityLookup:
                    return printer;
                case PrimitiveType.EntityStorageInfoLookup:
                    return printer.PrintBeginLine($"global::Unity.Entities.EntityStorageInfoLookup ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.SharedComponentLookup: // will use a EntityStorageInfoLookup to get the chunk and then use the Scth to decay to the SharedComponent.
                    return printer.PrintBeginLine("global::Unity.Entities.SharedComponentTypeHandle<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.DynamicBuffer:
                    return printer.PrintBeginLine("global::Unity.Entities.DynamicBuffer").Print("<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.EnabledRef:
                    return printer.PrintBeginLine("global::Unity.Entities.EnabledRef").Print(Bind.IsReadOnly ? "RO" : "RW").Print("<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.Ref:
                    return printer.PrintBeginLine("global::Unity.Entities.Ref").Print(Bind.IsReadOnly ? "RO" : "RW").Print("<").Print(Bind.ComponentTypeName).Print("> ").Print(fieldName).PrintEndLine(";");
                case PrimitiveType.SharedComponent:
                    return printer.PrintBeginLine("public ").Print(Bind.ComponentTypeName).Print(" ").Print(fieldName).PrintEndLine(";");
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Print the code representing the primary decay of this primitive using a specific binding.
        ///
        /// for lookup primitives such as ComponentLookup<T> and BufferLookup<T>
        ///     * The primitive will decay to Ref??<T>
        ///     * The catalyst symbol will be the primitive "Entity entity".
        ///     * ex: "this.{fieldName}.GetRefRO<T>(entity)"
        ///
        /// for iteration primitives such as ComponentTypeHandle<T>, BufferTypeHandle<T> and EntityTypeHandle:
        ///     * The primitive will decay to NativeArray<T> / NativeArray<Entity>
        ///     * The catalyst symbol will be the primitive "ArchetypeChunk chunk".
        ///     * ex: "chunk.GetNativeArray(this.{fieldName})"
        ///
        /// for chunk primitives such as NativeArray<T> and BufferAccessor<T>:
        ///     * The primitive will decay to Ref??<T>
        ///     * The catalyst symbol will be the primitive "int index".
        ///     * ex: "new RefRO(this.{fieldName}, index)"
        ///
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="fieldName">The field name of the declared primitive.</param>
        /// <param name="Bind">The binding used for the primitive</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer PrimaryDecay(Printer printer, string fieldName, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                    return printer;
                case PrimitiveType.BufferAccessor:
                    return printer.Print("this.").Print(fieldName).Print("[index]");
                case PrimitiveType.ComponentNativeArray:
                    if (Bind.IsOptional)
                        return RefTypeName(printer, Bind.IsReadOnly, Bind.ComponentTypeName).Print(".Optional(this.").Print(fieldName).Print(", index)");
                    return printer.Print("new ").PrintWith(RefTypeName(printer, Bind.IsReadOnly, Bind.ComponentTypeName)).Print("(this.").Print(fieldName).Print(", index)");
                case PrimitiveType.EnabledMask:
                    return printer.Print("this.").Print(fieldName)
                        .Print(".Get").PrintIf(Bind.IsOptional, "Optional").Print("EnabledRef")
                        .Print(Bind.IsReadOnly ? "RO<" : "RW<").Print(Bind.ComponentTypeName).Print(">(index)");
                case PrimitiveType.EntityNativeArray:
                    return printer.Print("this.").Print(fieldName).Print("[index]");
                case PrimitiveType.Entity:
                    return printer;
                case PrimitiveType.BufferTypeHandle:
                    return printer.Print("chunk.GetBufferAccessor(ref this.").Print(fieldName).Print(")");
                case PrimitiveType.ComponentTypeHandle:
                    return printer.Print("chunk.GetNativeArray(ref this.").Print(fieldName).Print(")");
                case PrimitiveType.EntityTypeHandle:
                    return printer.Print("chunk.GetNativeArray(this.").Print(fieldName).Print(")");
                case PrimitiveType.SharedComponentTypeHandle:
                    if (Bind.IsOptional)
                        printer.Print("!chunk.Has<").Print(Bind.ComponentTypeName).Print(">(this.").Print(fieldName).Print(") ? default : ");
                    return printer.Print("chunk.GetSharedComponent<").Print(Bind.ComponentTypeName).Print(">(this.").Print(fieldName).Print(")");
                case PrimitiveType.BufferLookup:
                    return printer.Print("this.").Print(fieldName).Print("[entity]");
                case PrimitiveType.ComponentLookup:
                    return printer.Print("this.").Print(fieldName).Print(".GetRef").Print(Bind.IsReadOnly ? "RO" : "RW")
                        .PrintIf(Bind.IsOptional, "Optional").Print("(entity").PrintIf(!Bind.IsReadOnly, string.Empty).Print(")");
                case PrimitiveType.EntityLookup:
                    return printer.Print("entity");
                case PrimitiveType.EntityStorageInfoLookup:
                    return printer.Print("this.").Print(fieldName).Print("[entity].Chunk");
                case PrimitiveType.SharedComponentLookup:
                    if (Bind.IsOptional)
                        printer.Print("!chunk.Has<").Print(Bind.ComponentTypeName).Print(">(this.").Print(fieldName).Print(") ? default : ");
                    return printer.Print("chunk.GetSharedComponent<").Print(Bind.ComponentTypeName).Print(">(this.").Print(fieldName).Print(")");
                case PrimitiveType.DynamicBuffer:
                case PrimitiveType.EnabledRef:
                case PrimitiveType.Ref:
                    return printer;
                case PrimitiveType.SharedComponent:
                    return printer.Print(fieldName);
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Print the code representing the decay of this primitive using a specific binding.
        /// When a primitive only has 1 decay, use Primary Decay
        /// <see cref="PrimaryDecay"/>
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="fieldName">The field name of the declared primitive.</param>
        /// <param name="Bind">The binding used for the primitive</param>
        /// <returns></returns>
        public Printer Decay(Printer printer, string fieldName, PrimitiveBinding Bind)
        {
            return PrimaryDecay(printer, fieldName, Bind);
        }

        /// <summary>
        /// Print the code representing the secondary decay of this primitive.
        ///
        /// for lookup primitives such as ComponentLookup<T> and BufferLookup<T>
        ///     * The primitive will decay to EnabledRef??<T>
        ///     * The catalyst symbol will be the primitive "Entity entity".
        ///     * ex: "this.{fieldName}.GetEnabledRefRO<T>(entity)"
        ///
        /// for iteration primitives such as ComponentTypeHandle<T>:
        ///     * The primitive will decay to EnabledMask
        ///     * The catalyst symbol will be the primitive "ArchetypeChunk chunk".
        ///     * ex: "chunk.GetEnabledMask(this.{fieldName})"
        ///
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="fieldName">The field name of the declared primitive.</param>
        /// <param name="Bind">The binding used for the primitive</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer SecondaryDecay(Printer printer, string fieldName, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                case PrimitiveType.BufferAccessor:
                case PrimitiveType.ComponentNativeArray:
                case PrimitiveType.EnabledMask:
                case PrimitiveType.EntityNativeArray:
                case PrimitiveType.Entity:
                case PrimitiveType.BufferTypeHandle:
                    return printer;

                case PrimitiveType.ComponentTypeHandle:
                    return printer.Print("chunk.GetEnabledMask(ref ").Print(fieldName).Print(")");
                case PrimitiveType.EntityTypeHandle:
                    return printer;
                case PrimitiveType.SharedComponentTypeHandle:
                    return printer;
                case PrimitiveType.BufferLookup:
                    return printer;
                case PrimitiveType.ComponentLookup:
                    return printer.Print("this.").Print(fieldName).Print(".GetEnabledRef").Print(Bind.IsReadOnly ? "RO" : "RW").PrintIf(Bind.IsOptional, "Optional")
                        .Print("<")
                        .Print(Bind.ComponentTypeName)
                        .Print(">")
                        .Print("(entity)");

                case PrimitiveType.EntityLookup:
                    return printer;
                case PrimitiveType.EntityStorageInfoLookup:
                case PrimitiveType.SharedComponentLookup:
                case PrimitiveType.DynamicBuffer:
                case PrimitiveType.EnabledRef:
                case PrimitiveType.Ref:
                    return printer;
                case PrimitiveType.SharedComponent:
                    return printer;
            }
            throw new InvalidOperationException();
        }


        /// <summary>
        /// Print the code representing the construction of the primitive field
        /// Available symbols:
        ///     "state" : the current SystemState
        ///     "this"  : Aspect.TypeHandle or Aspect.Lookup
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="fieldName">The field name of the declared primitive.</param>
        /// <param name="Bind">The binding used for the primitive</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer ConstructFromState(Printer printer, string fieldName, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                    return printer;
                case PrimitiveType.BufferAccessor:
                    return printer;
                case PrimitiveType.ComponentNativeArray:
                    return printer;
                case PrimitiveType.EnabledMask:
                    return printer;
                case PrimitiveType.EntityNativeArray:
                    return printer;
                case PrimitiveType.Entity:
                    return printer;
                case PrimitiveType.BufferTypeHandle:
                    return printer.PrintLine($"this.{fieldName} = state.GetBufferTypeHandle<{Bind.ComponentTypeName}>({(Bind.IsReadOnly ? "true" : "false")});");
                case PrimitiveType.ComponentTypeHandle:
                    return printer.PrintLine($"this.{fieldName} = state.GetComponentTypeHandle<{Bind.ComponentTypeName}>({(Bind.IsReadOnly ? "true" : "false")});");
                case PrimitiveType.EntityTypeHandle:
                    return printer.PrintLine($"this.{fieldName} = state.GetEntityTypeHandle();");
                case PrimitiveType.SharedComponentTypeHandle:
                    return printer.PrintLine($"this.{fieldName} = state.GetSharedComponentTypeHandle<{Bind.ComponentTypeName}>();");
                case PrimitiveType.BufferLookup:
                    return printer.PrintBeginLine($"this.").Print(fieldName)
                        .Print(" = state.GetBufferLookup<").Print(Bind.ComponentTypeName).Print(">(")
                        .Print(Bind.IsReadOnly ? "true" : "false").PrintEndLine(");");
                case PrimitiveType.ComponentLookup:
                    return printer.PrintLine($"this.{fieldName} = state.GetComponentLookup<{Bind.ComponentTypeName}>({(Bind.IsReadOnly ? "true" : "false")});");
                case PrimitiveType.EntityLookup:
                    return printer;
                case PrimitiveType.EntityStorageInfoLookup:
                    return printer.PrintLine($"this.{fieldName} = state.GetEntityStorageInfoLookup();");
                case PrimitiveType.SharedComponentLookup: // use a Scth to do a lookup with a EntityStorageInfoLookup
                    return printer.PrintLine($"this.{fieldName} = state.GetSharedComponentTypeHandle<{Bind.ComponentTypeName}>();");
                case PrimitiveType.DynamicBuffer:
                    return printer;
                case PrimitiveType.EnabledRef:
                    return printer;
                case PrimitiveType.Ref:
                    return printer;
                case PrimitiveType.SharedComponent:
                    return printer;
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Print the code representing the update of the primitive field
        /// Available symbols:
        ///     "state" : the current SystemState
        ///     "this"  : Aspect.TypeHandle or Aspect.Lookup
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="fieldName">The field name of the declared primitive.</param>
        /// <param name="Bind">The binding used for the primitive</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer Update(Printer printer, string fieldName, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                    return printer;
                case PrimitiveType.BufferAccessor:
                    return printer;
                case PrimitiveType.ComponentNativeArray:
                    return printer;
                case PrimitiveType.EnabledMask:
                    return printer;
                case PrimitiveType.EntityNativeArray:
                    return printer;
                case PrimitiveType.Entity:
                    return printer;
                case PrimitiveType.BufferTypeHandle:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.ComponentTypeHandle:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.EntityTypeHandle:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.SharedComponentTypeHandle:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.BufferLookup:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.ComponentLookup:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.EntityLookup:
                    return printer;
                case PrimitiveType.EntityStorageInfoLookup:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.SharedComponentLookup:
                    return printer.PrintLine($"this.{fieldName}.Update(ref state);");
                case PrimitiveType.DynamicBuffer:
                    return printer;
                case PrimitiveType.EnabledRef:
                    return printer;
                case PrimitiveType.Ref:
                    return printer;
                case PrimitiveType.SharedComponent:
                    return printer;
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Print a human readable string representing the primitive
        /// </summary>
        /// <param name="printer"></param>
        /// <param name="Bind"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Printer Print(Printer printer, PrimitiveBinding Bind)
        {
            switch (Type)
            {
                case PrimitiveType.Nothing:
                    return printer;
                case PrimitiveType.BufferAccessor:
                    return printer.Debug.Print("BufferAccessor{").Print(Bind).Print("}");
                case PrimitiveType.ComponentNativeArray:
                    return printer.Debug.Print("ComponentNativeArray{").Print(Bind).Print("}");
                case PrimitiveType.EnabledMask:
                    return printer.Debug.Print("EnabledMask{").Print(Bind).Print("}");
                case PrimitiveType.EntityNativeArray:
                    return printer.Debug.Print("EntityNativeArray");
                case PrimitiveType.Entity:
                    return printer.Debug.Print("Entity");
                case PrimitiveType.BufferTypeHandle:
                    return printer.Debug.Print("BufferTypeHandle{").Print(Bind).Print("}");
                case PrimitiveType.ComponentTypeHandle:
                    return printer.Debug.Print("ComponentTypeHandle{").Print(Bind).Print("}");
                case PrimitiveType.EntityTypeHandle:
                    return printer.Debug.Print("EntityTypeHandle");
                case PrimitiveType.SharedComponentTypeHandle:
                    return printer.Debug.Print("SharedComponentTypeHandle{").Print(Bind).Print("}");
                case PrimitiveType.BufferLookup:
                    return printer.Debug.Print("BufferLookup{").Print(Bind).Print("}");
                case PrimitiveType.ComponentLookup:
                    return printer.Debug.Print("ComponentLookup{").Print(Bind).Print("}");
                case PrimitiveType.EntityLookup:
                    return printer.Debug.Print("EntityLookup");
                case PrimitiveType.EntityStorageInfoLookup:
                    return printer.Debug.Print("EntityStorageInfoLookup");
                case PrimitiveType.SharedComponentLookup:
                    return printer.Debug.Print("SharedComponentLookup");
                case PrimitiveType.DynamicBuffer:
                    return printer.Debug.Print("DynamicBuffer{").Print(Bind).Print("}");
                case PrimitiveType.EnabledRef:
                    return printer.Debug.Print("EnabledRef{").Print(Bind).Print("}");
                case PrimitiveType.Ref:
                    return printer.Debug.Print("Ref{").Print(Bind).Print("}");
            }
            throw new InvalidOperationException();
        }

        public void Print(Printer printer) => Print(printer, default);

    }

    /// <summary>
    /// An access primitive is composed of multiple primitives that follows these decay sequences:
    /// Primary decay sequences:
    ///     Lookup -> ReferenceFromPrimaryDecay
    ///     Iteration -> ChunkFromPrimaryDecay -> ReferenceFromPrimaryDecay
    /// Secondary decay sequences:
    ///     Lookup -> ReferenceFromSecondaryDecay
    ///     Iteration -> ChunkFromSecondaryDecay -> ReferenceFromSecondaryDecay
    /// </summary>
    public struct AccessPrimitive : IPrintable
    {
        /// <summary>
        /// Primitive to do lookup to either
        /// ReferenceFromPrimaryDecay or ReferenceFromSecondaryDecay.
        /// </summary>
        public DotsPrimitive Lookup;

        /// <summary>
        /// Primitive to do iterations on multiple chunks of
        /// either ChunkFromPrimaryDecay or ChunkFromSecondaryDecay.
        /// Will be a XyzTypeHandle
        /// </summary>
        public DotsPrimitive Iteration;

        /// <summary>
        /// Chunk primitive from the primary decay of the Iteration primitive
        /// </summary>
        public DotsPrimitive ChunkFromPrimaryDecay;

        /// <summary>
        /// Reference primitive from the primary decay of the Lookup primitive or
        /// the decay of ChunkFromPrimaryDecay
        /// </summary>
        public DotsPrimitive ReferenceFromPrimaryDecay;

        /// <summary>
        /// Chunk primitive from the secondary decay of the Iteration primitive
        /// </summary>
        public DotsPrimitive ChunkFromSecondaryDecay;

        /// <summary>
        /// Reference primitive from the primary decay of the Lookup primitive or
        /// the decay of ChunkFromPrimaryDecay
        /// </summary>
        public DotsPrimitive ReferenceFromSecondaryDecay;

        /// <summary>
        /// Some primitives like ComponentLookup and ComponentTypeHandle requires a binding
        /// to a component typename, a read-write and an optional flags.
        /// </summary>
        public PrimitiveBinding Bind;

        /// <summary>
        /// Whether this access primitive may add a ComponentType to the entity query.
        /// e.g. Data primitives like Unity.Entities.Entity do not add to the entity query
        /// </summary>
        public bool IsQueryComponent;

        /// <summary>
        /// String to use to differentiate this Access Primitive from another
        /// </summary>
        public string Tag;

        /// <summary>
        /// Whether this Access Primitive has a primary decay sequence.
        /// </summary>
        public bool HasPrimaryDecaySequence => !ReferenceFromPrimaryDecay.IsNothing;

        /// <summary>
        /// Whether this Access Primitive has a secondary decay sequence.
        /// </summary>
        public bool HasSecondaryDecaySequence => !ReferenceFromSecondaryDecay.IsNothing;

        /// <summary>
        /// Whether this Access Primitive has any decay sequence.
        /// Primitive without any decay sequence are invalid and will not output any code.
        /// </summary>
        public bool HasAnyDecaySequences => HasPrimaryDecaySequence | HasSecondaryDecaySequence;

        /// <summary>
        /// Type name of the primary Reference primitive, including binding
        /// e.g "RefRO<MyComponent>"
        /// </summary>
        public string PrimaryReferenceTypename => ReferenceFromPrimaryDecay.TypeName(Printer.Default, Bind).Result;

        /// <summary>
        /// Type name of the secondary Reference primitive, including binding
        /// e.g "EnabledRefRO<MyComponent>"
        /// </summary>
        public string SecondaryReferenceTypename => ReferenceFromSecondaryDecay.TypeName(Printer.Default, Bind).Result;

        /// <summary>
        /// If this access primitive requires a EntityStorageInfoLookup primitive for lookups.
        /// </summary>
        public bool RequiresEntityStorageInfoLookup;

        /// <summary>
        /// Merge other Access primitive into this primitive.
        /// Selects the first sequences that are set in this or the other AccessPrimitive
        /// for each primary and secondary sets of primitives.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public AccessPrimitive Merge(AccessPrimitive other)
            => new AccessPrimitive
            {
                Lookup = Lookup,
                Iteration = Iteration,
                ChunkFromPrimaryDecay = HasPrimaryDecaySequence ? ChunkFromPrimaryDecay : other.ChunkFromPrimaryDecay,
                ReferenceFromPrimaryDecay = HasPrimaryDecaySequence ? ReferenceFromPrimaryDecay : other.ReferenceFromPrimaryDecay,
                ChunkFromSecondaryDecay = HasSecondaryDecaySequence ? ChunkFromSecondaryDecay : other.ChunkFromSecondaryDecay,
                ReferenceFromSecondaryDecay = HasSecondaryDecaySequence ? ReferenceFromSecondaryDecay : other.ReferenceFromSecondaryDecay,
                RequiresEntityStorageInfoLookup = RequiresEntityStorageInfoLookup || other.RequiresEntityStorageInfoLookup,
                Bind = PrimitiveBinding.Merge(Bind, other.Bind),
                IsQueryComponent = IsQueryComponent | other.IsQueryComponent
            };


        /// <summary>
        /// Print a human readable text representing the state of this struct
        /// </summary>
        /// <param name="printer"></param>
        public void Print(Printer printer)
        {
            var scope = printer.ScopePrinter("AccessPrimitive {");
            {
                var list = scope.PrintBeginLine().AsListPrinter(", ").AsMultiline;
                list.NextItemPrinter().Debug.PrintKeyValue(".Bind", Bind);
                list.NextItemPrinter().Debug.PrintKeyValue(".Primary.Lookup", Lookup.Type.ToString());
                list.NextItemPrinter().Debug.PrintKeyValue(".Primary.Iteration", Iteration.Type.ToString());
                if (HasPrimaryDecaySequence)
                {
                    list.NextItemPrinter().Debug.PrintKeyValue(".Primary.Chunk", ChunkFromPrimaryDecay.Type.ToString());
                    list.NextItemPrinter().Debug.PrintKeyValue(".Primary.Reference", ReferenceFromPrimaryDecay.Type.ToString());
                }
                if (HasSecondaryDecaySequence)
                {
                    list.NextItemPrinter().Debug.PrintKeyValue(".Secondary.Chunk", ChunkFromSecondaryDecay.Type.ToString());
                    list.NextItemPrinter().Debug.PrintKeyValue(".Secondary.Reference", ReferenceFromSecondaryDecay.Type.ToString());
                }
                list.NextItemPrinter().Debug.PrintKeyValue(".RequiresEntityStorageInfoLookup", RequiresEntityStorageInfoLookup.ToString());
                list.NextItemPrinter().Debug.PrintKeyValue(".IsQueryComponent", IsQueryComponent.ToString());
                scope.PrintEndLine();
            }
            printer.CloseScope(scope, "}");
        }
    }
}
