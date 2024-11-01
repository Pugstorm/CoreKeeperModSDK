using System;
using System.Linq;
using Unity.Entities.SourceGen.Aspect;
using Unity.Entities.SourceGen.Common;

public static class AspectSyntaxFactory
{
    public static bool AspectNeedConstructor(this AspectDefinition aspect)
        => aspect.HasEntityField
           || aspect.FieldsNeedConstruction.Any()
           || (aspect.NestedAspects != null && aspect.NestedAspects.Any(x=> AspectNeedConstructor(x.Definition)));

    public static Printer PrintConstructor(Printer printer, AspectDefinition aspect)
    {
        if (!AspectNeedConstructor(aspect)) return printer;

        printer.PrintLine("/// <summary>");
        printer.PrintLine("/// Construct an instance of the enclosing aspect from all required data references.");
        printer.PrintLine("/// </summary>");
        var printerParamList = printer.PrintBeginLine("public ").Print(aspect.Name).Print("(").AsListPrinter(", ").AsMultilineIndented;
        {
            // Constructor Parameters
            foreach (var param in aspect.PrimitivesRouter.AllConstructorParameters)
                printerParamList.NextItemPrinter().Print(param.TypeName).Print(" ").Print(param.ParameterName);
            printer.PrintEndLine(")");
        }

        var scope = printer.PrintBeginLine().ScopePrinter("{");
        {
            // Constructor body

            // Construct all AspectFields declared by this aspect.
            foreach (var (field, param) in aspect.PrimitivesRouter.GetRoutedConstructorParametersFor(aspect))
                scope.PrintLine($"this.{field.FieldName} = {param.ParameterName};");

            foreach (var field in aspect.FieldsRequiringDefaultConstruction)
                scope.PrintBeginLine($"this.{field.FieldName} = default;");

            // Construct all nested aspects
            if (aspect.NestedAspects != null)
            {
                foreach (var nestedAspect in aspect.NestedAspects)
                {
                    // Route this aspect constructor parameters to the nested aspect constructor arguments.
                    // emits something like:
                    //      this.m_MyNestedAspect = new global::MyNestedAspect(myCompARef, myCompBRef, myCompCDynamicBuffer);
                    var constructorArgumentList = scope.PrintBeginLine("this.")
                        .Print(nestedAspect.AspectField.FieldName).Print(" = new ")
                        .Print(nestedAspect.Definition.FullName).Print("(").AsListPrinter(", ").AsMultilineIndented;

                    // List all constructor of arguments following the constructor parameters of the nested aspect.
                    foreach (var param in nestedAspect.Definition.PrimitivesRouter.AllConstructorParameters)
                    {
                        // Get the AspectField that named the constructor parameter that routes to the current argument
                        var originalField = aspect.PrimitivesRouter.GetRoutedFieldForAlias(param.DeclaringAspectField, out var tag);
                        constructorArgumentList.NextItemPrinter().Print(originalField.GetParameterName(tag));
                    }

                    scope.PrintEndLine(");");
                }
            }
        }
        printer.CloseScope(scope, "}").PrintEndLine();
        return printer;
    }

    static Printer PrintAddComponentTypes(Printer printer, AspectDefinition aspect)
    {
        printer.PrintLine("var allRequiredComponentsInAspect =");
        printer.IncreasedIndent();
        printer.PrintBeginLine("new global::Unity.Collections.LowLevel.Unsafe.UnsafeList<global::Unity.Entities.ComponentType>(initialCapacity: 8, allocator: global::Unity.Collections.Allocator.Temp, options: global::Unity.Collections.NativeArrayOptions.ClearMemory)");

        printer.OpenScope();
        foreach (var q in aspect.PrimitivesRouter.QueryBindings)
            printer.PrintBeginLine("global::Unity.Entities.ComponentType.").Print(q.IsReadOnly ? "ReadOnly<" : "ReadWrite<").Print(q.ComponentTypeName).PrintEndLine(">(),");
        printer.CloseScope("};");
        printer.DecreasedIndent();
        printer.PrintLine("global::Unity.Entities.Internal.InternalCompilerInterface.MergeWith(ref all, ref allRequiredComponentsInAspect);");
        printer.PrintLine("allRequiredComponentsInAspect.Dispose();");

        return printer;
    }

    static Printer PrintAddComponentTypesToSpan(Printer printer, AspectDefinition aspect)
    {
        var addComponents =
            aspect.PrimitivesRouter.QueryBindings
                .Select((q, i) =>
                    $"componentTypes[{i}] = {(q.IsReadOnly ? $"global::Unity.Entities.ComponentType.ReadOnly<{q.ComponentTypeName}>();" : $"global::Unity.Entities.ComponentType.ReadWrite<{q.ComponentTypeName}>();")}")
                .SeparateBy($"{Environment.NewLine}				");

        return printer.PrintBeginLine(addComponents).PrintBeginLine(Environment.NewLine);
    }

    public static string GenerateAspectSource(AspectDefinition aspect)
    {
        string interfaceToImplement = "global::Unity.Entities.IAspect";
        if (!aspect.IsIAspectCreateCorrectlyImplementedByUser)
            interfaceToImplement += $", global::Unity.Entities.IAspectCreate<{aspect.Name}>";

        // Print the full scope path of namespace/class/struct to the aspect declaration
        SyntaxNodeScopePrinter aspectNestedScopePrinter = new SyntaxNodeScopePrinter(Printer.DefaultLarge, aspect.SourceSyntaxNodes.First().Parent);
        aspectNestedScopePrinter.PrintOpen();
        Printer printer = aspectNestedScopePrinter.Printer;
        printer.PrintEndLine();

        // Print struct declaration: "public readonly struct MyAspect : IAspect"
        printer.PrintBeginLine()
            .AsListPrinter(" ").PrintAll(aspect.SourceSyntaxNodes.First().Modifiers.Select(token => token.ToString()))
            .Printer.Print(" struct ").Print(aspect.Name).Print(" : ").Print(interfaceToImplement);
        {
            printer.OpenScope();
            // Print Aspect's Constructor
            PrintConstructor(printer, aspect);
            // Print IAspectCreate implementation if needed
            if (!aspect.IsIAspectCreateCorrectlyImplementedByUser)
            {
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Create an instance of the enclosing aspect struct pointing at a specific entity's components data.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""entity"">The entity to create the aspect struct from.</param>");
                printer.PrintLine(@"/// <param name=""systemState"">The system state from which data is extracted.</param>");
                printer.PrintLine(@"/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>");
                printer.PrintBeginLine("public ").Print(aspect.Name).Print(" CreateAspect(global::Unity.Entities.Entity entity, ref global::Unity.Entities.SystemState systemState)");
                {
                    printer.OpenScope();
                    printer.PrintLine("var lookup = new Lookup(ref systemState);");
                    printer.PrintLine("return lookup[entity];");
                    printer.CloseScope();
                }
            }

            printer.PrintEndLine();
            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// Add component requirements from this aspect into all archetype lists.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintLine(@"/// <param name=""all"">Archetype ""all"" component requirements.</param>");
            printer.PrintBeginLine(
                    "public void AddComponentRequirementsTo(ref global::Unity.Collections.LowLevel.Unsafe.UnsafeList<global::Unity.Entities.ComponentType> all)")
                .OpenScope()
                .PrintWith(x => PrintAddComponentTypes(x, aspect))
                .CloseScope();

            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// Get the number of required (i.e. non-optional) components contained in this aspect.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintLine(@"/// <returns>The number of required (i.e. non-optional) components contained in this aspect.</returns>");
            printer.PrintBeginLine($"public static int GetRequiredComponentTypeCount() => {aspect.PrimitivesRouter.QueryBindings.Count};{Environment.NewLine}");

            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// Add component requirements from this aspect into the provided span.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintLine(@"/// <param name=""componentTypes"">The span to which all required components in this aspect are added.</param>");
            printer.PrintBeginLine(
                    "public static void AddRequiredComponentTypes(ref global::System.Span<global::Unity.Entities.ComponentType> componentTypes)")
                .OpenScope()
                .PrintWith(x => PrintAddComponentTypesToSpan(x, aspect))
                .CloseScope();

            // Print Aspect's Lookup primitive
            printer.PrintEndLine();
            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// A container type that provides access to instances of the enclosing Aspect type, indexed by <see cref=""Unity.Entities.Entity""/>.");
            printer.PrintLine(@"/// Equivalent to <see cref=""global::Unity.Entities.ComponentLookup{T}""/> but for aspect types.");
            printer.PrintLine(@"/// Constructed from an system state via its constructor.");
            printer.PrintBeginLine($"public struct Lookup : global::Unity.Entities.Internal.InternalCompilerInterface.IAspectLookup<{aspect.Name}>");
            {
                printer.OpenScope();
                // Declare all lookup primitives
                aspect.PrimitivesRouter.LookupDeclare(printer);
                // Lookup constructor
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Create the aspect lookup from an system state.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""state"">The system state to create the aspect lookup from.</param>");
                printer.PrintBeginLine("public Lookup(ref global::Unity.Entities.SystemState state)");
                {
                    printer.OpenScope();
                    aspect.PrimitivesRouter.LookupConstructFromState(printer);
                    printer.CloseScope();
                }
                // Lookup Update
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Update the lookup container.");
                printer.PrintLine(@"/// Must be called every frames before using the lookup.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""state"">The system state the aspect lookup was created from.</param>");
                printer.PrintBeginLine("public void Update(ref global::Unity.Entities.SystemState state)");
                {
                    printer.OpenScope();
                    aspect.PrimitivesRouter.LookupUpdate(printer);
                    printer.CloseScope();
                }
                // Lookup Entity Indexer
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Get an aspect instance pointing at a specific entity's components data.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""entity"">The entity to create the aspect struct from.</param>");
                printer.PrintLine(@"/// <returns>Instance of the aspect struct pointing at a specific entity's components data.</returns>");
                printer = printer.PrintBeginLine("public ").Print(aspect.Name).Print(" this[global::Unity.Entities.Entity entity]")
                    .OpenScope().PrintBeginLine("get").OpenScope();
				aspect.PrimitivesRouter.LookupDecay(printer, aspect.Name);
				printer = printer.CloseScope().CloseScope();
                // close Lookup struct
                printer.CloseScope();
            }
            // Print Aspect's Chunk Primitive
            printer.PrintEndLine();
            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// Chunk of the enclosing aspect instances.");
            printer.PrintLine(@"/// the aspect struct itself is instantiated from multiple component data chunks.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintBeginLine("public struct ResolvedChunk");
            {
                printer.OpenScope();
                // Declare all Chunk Primitives
                aspect.PrimitivesRouter.ChunkDeclare(printer);
                // Chunk int Indexer
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Get an aspect instance pointing at a specific entity's component data in the chunk index.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""index""></param>");
                printer.PrintLine(@"/// <returns>Aspect for the entity in the chunk at the given index.</returns>");
                printer.PrintBeginLine("public ").Print(aspect.Name).PrintEndLine(" this[int index]");
                printer.WithIncreasedIndent().PrintBeginLine("=> new ").Print(aspect.Name).Print("(").PrintWith(
                    // Chunk Decay to References
                    aspect.PrimitivesRouter.ChunkDecay(printer)
                ).PrintEndLine(");");
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Number of entities in this chunk.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine("public int Length;");
                printer.CloseScope();
            }
            // Print Aspect's TypeHandle Primitive
            printer.PrintEndLine();
            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// A handle to the enclosing aspect type, used to access a <see cref=""ResolvedChunk""/>'s components data in a job.");
            printer.PrintLine(@"/// Equivalent to <see cref=""Unity.Entities.ComponentTypeHandle{T}""/> but for aspect types.");
            printer.PrintLine(@"/// Constructed from an system state via its constructor.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintBeginLine("public struct TypeHandle");
            {
                printer.OpenScope();
                // Declare all Iteration Primitives
                aspect.PrimitivesRouter.TypeHandleDeclare(printer);
                // TypeHandle Constructor
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Create the aspect type handle from an system state.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""state"">System state to create the type handle from.</param>");
                printer.PrintBeginLine("public TypeHandle(ref global::Unity.Entities.SystemState state)");
                {
                    printer.OpenScope();
                    aspect.PrimitivesRouter.TypeHandleConstructFromState(printer);
                    printer.CloseScope();
                }
                // TypeHandle Update
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Update the type handle container.");
                printer.PrintLine(@"/// Must be called every frames before using the type handle.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""state"">The system state the aspect type handle was created from.</param>");
                printer.PrintBeginLine("public void Update(ref global::Unity.Entities.SystemState state)");
                {
                    printer.OpenScope();
                    aspect.PrimitivesRouter.TypeHandleUpdate(printer);
                    printer.CloseScope();
                }
                // TypeHandle Decay to Chunk
                printer.PrintEndLine();
                printer.PrintLine(@"/// <summary>");
                printer.PrintLine(@"/// Get the enclosing aspect's <see cref=""ResolvedChunk""/> from an <see cref=""global::Unity.Entities.ArchetypeChunk""/>.");
                printer.PrintLine(@"/// </summary>");
                printer.PrintLine(@"/// <param name=""chunk"">The ArchetypeChunk to extract the aspect's ResolvedChunk from.</param>");
                printer.PrintLine(@"/// <returns>A ResolvedChunk representing all instances of the aspect in the chunk.</returns>");
                printer.PrintBeginLine("public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk chunk)");
                {
                    printer.OpenScope();
                    printer.PrintLine("ResolvedChunk resolved;");
                    aspect.PrimitivesRouter.TypeHandleDecay(printer);
                    printer.PrintLine("resolved.Length = chunk.Count;");
                    printer.PrintLine("return resolved;");
                    printer.CloseScope();
                }
                printer.CloseScope();
            }
            printer.PrintEndLine();
            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// Enumerate the enclosing aspect from all entities in a query.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintLine(@"/// <param name=""query"">The entity query to enumerate.</param>");
            printer.PrintLine(@"/// <param name=""typeHandle"">The aspect's enclosing type handle.</param>");
            printer.PrintLine(@"/// <returns>An enumerator of all the entities instance of the enclosing aspect.</returns>");
            printer.PrintLine(@$"public static Enumerator Query(global::Unity.Entities.EntityQuery query, TypeHandle typeHandle) {{ return new Enumerator(query, typeHandle); }}");
            printer.PrintEndLine();
            printer.PrintLine(@"/// <summary>");
            printer.PrintLine(@"/// Enumerable and Enumerator of the enclosing aspect.");
            printer.PrintLine(@"/// </summary>");
            printer.PrintLine(@$"public struct Enumerator : global::System.Collections.Generic.IEnumerator<{aspect.Name}>, global::System.Collections.Generic.IEnumerable<{aspect.Name}>");
            printer.PrintLine(@$"{{");
            printer.PrintLine(@$"    ResolvedChunk                                _Resolved;");
            printer.PrintLine(@$"    global::Unity.Entities.Internal.InternalEntityQueryEnumerator _QueryEnumerator;");
            printer.PrintLine(@$"    TypeHandle                                   _Handle;");
            printer.PrintLine(@$"    internal Enumerator(global::Unity.Entities.EntityQuery query, TypeHandle typeHandle)");
            printer.PrintLine(@$"    {{");
            printer.PrintLine(@$"        _QueryEnumerator = new global::Unity.Entities.Internal.InternalEntityQueryEnumerator(query);");
            printer.PrintLine(@$"        _Handle = typeHandle;");
            printer.PrintLine(@$"        _Resolved = default;");
            printer.PrintLine(@$"    }}");
            printer.PrintEndLine();
            printer.PrintLine(@$"    /// <summary>");
            printer.PrintLine(@$"    /// Dispose of this enumerator.");
            printer.PrintLine(@$"    /// </summary>");
            printer.PrintLine(@$"    public void Dispose() {{ _QueryEnumerator.Dispose(); }}");
            printer.PrintEndLine();
            printer.PrintLine(@$"    /// <summary>");
            printer.PrintLine(@$"    /// Move to next entity.");
            printer.PrintLine(@$"    /// </summary>");
            printer.PrintLine(@$"    /// <returns>if this enumerator has not reach the end of the enumeration yet. Current is valid.</returns>");
            printer.PrintLine(@$"    public bool MoveNext()");
            printer.PrintLine(@$"    {{");
            printer.PrintLine(@$"        if (_QueryEnumerator.MoveNextHotLoop())");
            printer.PrintLine(@$"            return true;");
            printer.PrintLine(@$"        return MoveNextCold();");
            printer.PrintLine(@$"    }}");
            printer.PrintEndLine();
            printer.PrintLine(@$"    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            printer.PrintLine(@$"    bool MoveNextCold()");
            printer.PrintLine(@$"    {{");
            printer.PrintLine(@$"        var didMove = _QueryEnumerator.MoveNextColdLoop(out var chunk);");
            printer.PrintLine(@$"        if (didMove)");
            printer.PrintLine(@$"            _Resolved = _Handle.Resolve(chunk);");
            printer.PrintLine(@$"        return didMove;");
            printer.PrintLine(@$"    }}");
            printer.PrintEndLine();
            printer.PrintLine(@$"    /// <summary>");
            printer.PrintLine(@$"    /// Get current entity aspect.");
            printer.PrintLine(@$"    /// </summary>");
            printer.PrintLine(@$"    public {aspect.Name} Current {{");
            printer.PrintLine(@$"        get {{");
            printer.PrintLine(@$"            #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG");
            printer.PrintLine(@$"                _QueryEnumerator.CheckDisposed();");
            printer.PrintLine(@$"            #endif");
            printer.PrintLine(@$"                return _Resolved[_QueryEnumerator.IndexInChunk];");
            printer.PrintLine(@$"            }}");
            printer.PrintLine(@$"    }}");
            printer.PrintEndLine();
            printer.PrintLine(@$"    /// <summary>");
            printer.PrintLine(@$"    /// Get the Enumerator from itself as a Enumerable.");
            printer.PrintLine(@$"    /// </summary>");
            printer.PrintLine(@$"    /// <returns>An Enumerator of the enclosing aspect.</returns>");
            printer.PrintLine(@$"    public Enumerator GetEnumerator()  {{ return this; }}");
            printer.PrintEndLine();
            printer.PrintLine(@$"    void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotImplementedException();");
            printer.PrintLine(@$"    object global::System.Collections.IEnumerator.Current => throw new global::System.NotImplementedException();");
            printer.PrintLine(@$"    global::System.Collections.Generic.IEnumerator<{aspect.Name}> global::System.Collections.Generic.IEnumerable<{aspect.Name}>.GetEnumerator() => throw new global::System.NotImplementedException();");
            printer.PrintLine(@$"    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=> throw new global::System.NotImplementedException();");
            printer.PrintLine(@$"}}");
            printer.PrintEndLine();
            printer.PrintLine(@$"/// <summary>");
            printer.PrintLine(@$"/// Completes the dependency chain required for this aspect to have read access.");
            printer.PrintLine(@$"/// So it completes all write dependencies of the components, buffers, etc. to allow for reading.");
            printer.PrintLine(@$"/// </summary>");
            printer.PrintLine(@$"/// <param name=""state"">The <see cref=""global::Unity.Entities.SystemState""/> containing an <see cref=""global::Unity.Entities.EntityManager""/> storing all dependencies.</param>");
            printer.PrintBeginLine($"public void CompleteDependencyBeforeRO(ref global::Unity.Entities.SystemState state)");
            printer.OpenScope();
            aspect.PrimitivesRouter.PrintCompleteDependency(printer, true);
            printer.CloseScope();
            printer.PrintEndLine();
            printer.PrintLine(@$"/// <summary>");
            printer.PrintLine(@$"/// Completes the dependency chain required for this component to have read and write access.");
            printer.PrintLine(@$"/// So it completes all write dependencies of the components, buffers, etc. to allow for reading,");
            printer.PrintLine(@$"/// and it completes all read dependencies, so we can write to it.");
            printer.PrintLine(@$"/// </summary>");
            printer.PrintLine(@$"/// <param name=""state"">The <see cref=""global::Unity.Entities.SystemState""/> containing an <see cref=""global::Unity.Entities.EntityManager""/> storing all dependencies.</param>");
            printer.PrintBeginLine(@$"public void CompleteDependencyBeforeRW(ref global::Unity.Entities.SystemState state)");
            printer.OpenScope();
            aspect.PrimitivesRouter.PrintCompleteDependency(printer, false);
            printer.CloseScope();
        }
        printer.CloseScope();
        aspectNestedScopePrinter.PrintClose();
        return printer.Result;
    }
}
