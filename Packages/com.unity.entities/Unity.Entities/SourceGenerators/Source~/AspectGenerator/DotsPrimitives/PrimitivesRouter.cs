using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities.SourceGen.Aspect
{

    /// <summary>
    /// A DependentPrimitive represents a set of DotsPrimitive such as ComponentLookup<CompT>, ComponentTypeHandle<CompT>, NativeArray<CompT>, RefRO<CompT>
    /// on which any AspectField may depend on to perform their functionality.
    /// </summary>
    public struct DependentPrimitive : IPrintable
    {
        public AccessPrimitive Primitive;
        public AliasedDependencies Dependencies;
        public string PrimitiveFieldName => Dependencies.DeclaringField.InternalFieldName + Primitive.Tag;

        /// <summary>
        /// Print a human readable state of this DependentPrimitive
        /// </summary>
        /// <param name="printer"></param>
        public void Print(Printer printer)
        {
            var scope = printer.ScopePrinter("DependentPrimitive{");
            {
                var list = scope.PrintBeginLine().AsListPrinter(", ").AsMultiline;
                list.NextItemPrinter().Debug.PrintKeyValue(".Primitive", Primitive);
                list.NextItemPrinter().Debug.PrintKeyValue(".Dependencies", Dependencies);
                scope.PrintEndLine();
            }
            printer.CloseScope(scope, "}");
        }
    }

    /// <summary>
    /// Represent a parameter for the aspects constructors
    /// </summary>
    public struct ConstructorParameter
    {
        public string TypeName;
        public string NameTag;
        public string ParameterName => DeclaringAspectField.GetParameterName(NameTag);

        /// <summary>
        /// The Aspect field that declares the Reference Primitive (Ref/EnabledRef/DynamicBuffer/Entity) in the aspect struct
        /// </summary>
        public AspectField DeclaringAspectField;
        public ConstructorParameter(AspectField declaringAspectField, string tag, string typename)
        {
            DeclaringAspectField = declaringAspectField;
            TypeName = typename;
            NameTag = tag;
        }
    }

    /// <summary>
    /// Used to generate an aspect's ResolvedChunk, TypeHandle and Lookup
    /// Gathers fields and their dependencies to various dots primitives.
    /// Also provide methods that output code routing all the primitives
    /// from the constructor according to the dependency.
    /// </summary>
    public struct PrimitivesRouter : IPrintable
    {
        SortedDictionary<string, DependentPrimitive> DependentPrimitives;

        public static PrimitivesRouter Default => new PrimitivesRouter { DependentPrimitives = new SortedDictionary<string, DependentPrimitive>() };

        public bool HasEntityField => DependentPrimitives.ContainsKey("global::Unity.Entities.Entity");

        public bool HasAnyQueryComponents
            => DependentPrimitives.Values.Any(x => x.Primitive.IsQueryComponent);

        /// <summary>
        /// Set to an EntityStorageInfoLookup if any access primitive require one to perform its lookups.
        /// </summary>
        DotsPrimitive m_EntityStorageInfoLookup;

        /// <summary>
        /// Collection of PrimitiveBinding for each ComponentType that forms the aspect entity query.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyCollection<PrimitiveBinding> QueryBindings
        {
            get
            {
                return
                    _queryBindings ??=
                        DependentPrimitives.Values
                            .Where(v => v.Primitive.IsQueryComponent && !v.Primitive.Bind.IsOptional)
                            .Select(v => v.Primitive.Bind).ToArray();
            }
        }

        private PrimitiveBinding[] _queryBindings;

        /// <summary>
        /// List all the constructor parameters required to construct all the fields of an aspect
        /// </summary>
        public IEnumerable<ConstructorParameter> AllConstructorParameters
        {
            get
            {
                // for all DependentPrimitive, yield a constructor for each of their existing
                // decay sequences to the reference primitive used in the aspect.
                foreach (var depPrim in DependentPrimitives.Values)
                {
                    if (depPrim.Primitive.HasPrimaryDecaySequence)
                        yield return new ConstructorParameter(
                            depPrim.Dependencies.DeclaringField,
                            depPrim.Primitive.ReferenceFromPrimaryDecay.Tag,
                            depPrim.Primitive.PrimaryReferenceTypename);

                    if (depPrim.Primitive.HasSecondaryDecaySequence)
                        yield return new ConstructorParameter(
                            depPrim.Dependencies.DeclaringField,
                            depPrim.Primitive.ReferenceFromSecondaryDecay.Tag,
                            depPrim.Primitive.SecondaryReferenceTypename);
                }
            }
        }

        /// <summary>
        /// List the routed ConstructorParameter for each aspect fields other than nested aspects.
        /// Used to constructed each AspectField from the generated aspect's constructor parameters.
        /// </summary>
        /// <param name="aspect"></param>
        /// <returns>AspectField to construct, ConstructorParameter to construct from</returns>
        public IEnumerable<(AspectField, ConstructorParameter)> GetRoutedConstructorParametersFor(AspectDefinition aspect)
        {
            // for all resolved DependentPrimitive, for both decay sequence, yield an AspectField that need to be
            // constructed and a constructor parameter to construct the field from.
            foreach (var depPrim in DependentPrimitives.Values)
            {
                if (depPrim.Primitive.HasPrimaryDecaySequence
                    && depPrim.Dependencies.Original.Primary.AspectDefinition == aspect)
                    yield return (
                        depPrim.Dependencies.Original.Primary,
                        new ConstructorParameter(
                            depPrim.Dependencies.DeclaringField,
                            depPrim.Primitive.ReferenceFromPrimaryDecay.Tag,
                            depPrim.Primitive.PrimaryReferenceTypename));

                if (depPrim.Primitive.HasSecondaryDecaySequence
                    && depPrim.Dependencies.Original.Secondary.AspectDefinition == aspect)
                    yield return (
                        depPrim.Dependencies.Original.Secondary,
                        new ConstructorParameter(
                            depPrim.Dependencies.DeclaringField,
                            depPrim.Primitive.ReferenceFromSecondaryDecay.Tag,
                            depPrim.Primitive.SecondaryReferenceTypename));
            }
        }

        /// <summary>
        /// Find the AspectField responsible for naming the dealised primitives (e.g "RefRW<T> myUniqueRefT;") of a aliased AspectField.
        /// Used to route an aspect constructor parameters to its nested aspects constructor parameters.
        /// </summary>
        /// <param name="aliasField"></param>
        /// <param name="tag"></param>
        /// <returns>
        /// If the AspectField is an alias, returned the AspectField responsible for naming (e.g. "myUniqueRefT").
        /// If the AspectField is not aliased, will return the same field.
        /// </returns>
        /// <exception cref="InvalidOperationException"></exception>
        public AspectField GetRoutedFieldForAlias(AspectField aliasField, out string tag)
        {
            if (aliasField.TypeName != null && DependentPrimitives.TryGetValue(aliasField.TypeName, out var depPrim))
                foreach (var d in depPrim.Dependencies.AllSubDependency)
                {
                    if (d.Primary == aliasField)
                    {
                        tag = depPrim.Primitive.ReferenceFromPrimaryDecay.Tag;
                        return depPrim.Dependencies.Original.Primary;
                    }
                    if (d.Secondary == aliasField)
                    {
                        tag = depPrim.Primitive.ReferenceFromSecondaryDecay.Tag;
                        return depPrim.Dependencies.Original.Secondary;
                    }
                }
            throw new InvalidOperationException("All aspect fields should have an associated original field.");
        }
        #region Add Primitives

        public bool AddRef(AspectField dependentField, out AspectField conflictingFieldIfFails)
        {
            // Look for an existing Ref field in the existing dependencies. Ref is a Primary field dependency.
            if (TryGetExistingFieldPrimary(dependentField, out conflictingFieldIfFails))
                return false;
            AddDependency(PrimitiveType.Ref, dependentField.Bind, MakePrimaryDependency(dependentField));
            return true;
        }

        public bool AddEnabledRef(AspectField dependentField, out AspectField conflictingFieldIfFails)
        {
            // Look for an existing EnabledRef field in the existing dependencies. EnabledRef is a Secondary field dependency.
            if (TryGetExistingFieldSecondary(dependentField, out conflictingFieldIfFails))
                return false;
            AddDependency(PrimitiveType.EnabledRef, dependentField.Bind, MakeSecondaryDependency(dependentField));
            return true;
        }

        public bool AddDynamicBuffer(AspectField dependentField, out AspectField conflictingFieldIfFails)
        {
            // Look for an existing DynamicBuffer field in the existing dependencies. DynamicBuffer is a Primary field dependency.
            if (TryGetExistingFieldPrimary(dependentField, out conflictingFieldIfFails))
                return false;
            AddDependency(PrimitiveType.DynamicBuffer, dependentField.Bind, MakePrimaryDependency(dependentField));
            return true;
        }

        public bool AddEntity(AspectField dependentField, out AspectField conflictingFieldIfFails)
        {
            // Look for an existing Entity field in the existing dependencies. Entity is a Primary field dependency.
            if (TryGetExistingFieldPrimary(dependentField, out conflictingFieldIfFails))
                return false;
            AddDependency(PrimitiveType.Entity, dependentField.Bind, MakePrimaryDependency(dependentField));
            return true;
        }

        public bool AddSharedComponent(AspectField dependentField, out AspectField conflictingFieldIfFails)
        {
            conflictingFieldIfFails = default;
            if (DependentPrimitives.TryGetValue(dependentField.TypeName, out var depPrim)
                && depPrim.Dependencies.Original.Primary != null
                && depPrim.Dependencies.Original.Primary.AspectDefinition == dependentField.AspectDefinition)
            {
                conflictingFieldIfFails = depPrim.Dependencies.Original.Primary;
                return false;
            }

            AddDependency(PrimitiveType.SharedComponent, dependentField.Bind, MakePrimaryDependency(dependentField));
            return true;
        }

        /// <summary>
        /// Add a dependency to an AccessPrimitive needed by a reference primitive (an aspect field).
        /// A unique AccessPrimitive is created for each unique bind.ComponentTypeName.
        /// Dependencies that alias each other will get their AccessPrimitive merged into a unique one.
        /// </summary>
        /// <param name="referencePrimitive">What type of reference primitive the new dependency is for.</param>
        /// <param name="bind">The binding used for that reference primitive.</param>
        /// <param name="dependency">The dependency to add</param>
        /// <returns></returns>
        bool AddDependency(PrimitiveType referencePrimitive, PrimitiveBinding bind, AliasedDependencies dependency)
        {
            var desiredPrimitive = new AccessPrimitive
            {
                Bind = bind,
                IsQueryComponent = !dependency.DeclaringField.IsOptional
            };
            switch (referencePrimitive)
            {
                case PrimitiveType.Ref:
                    if (!dependency.DeclaringField.IsZeroSize)
                    {
                        desiredPrimitive.Lookup = new DotsPrimitive(PrimitiveType.ComponentLookup);
                        desiredPrimitive.Iteration = new DotsPrimitive(PrimitiveType.ComponentTypeHandle);
                        desiredPrimitive.ChunkFromPrimaryDecay = new DotsPrimitive(PrimitiveType.ComponentNativeArray);
                        desiredPrimitive.ReferenceFromPrimaryDecay = new DotsPrimitive(PrimitiveType.Ref);
                    }
                    desiredPrimitive.Tag = "CAc";
                    break;
                case PrimitiveType.EnabledRef:
                    desiredPrimitive.Lookup = new DotsPrimitive(PrimitiveType.ComponentLookup);
                    desiredPrimitive.Iteration = new DotsPrimitive(PrimitiveType.ComponentTypeHandle);
                    desiredPrimitive.ChunkFromSecondaryDecay = new DotsPrimitive(PrimitiveType.EnabledMask);
                    desiredPrimitive.ReferenceFromSecondaryDecay = new DotsPrimitive(PrimitiveType.EnabledRef);
                    desiredPrimitive.Tag = "CAc";
                    break;
                case PrimitiveType.DynamicBuffer:
                    desiredPrimitive.Lookup = new DotsPrimitive(PrimitiveType.BufferLookup);
                    desiredPrimitive.Iteration = new DotsPrimitive(PrimitiveType.BufferTypeHandle);
                    desiredPrimitive.ChunkFromPrimaryDecay = new DotsPrimitive(PrimitiveType.BufferAccessor);
                    desiredPrimitive.ReferenceFromPrimaryDecay = new DotsPrimitive(PrimitiveType.DynamicBuffer);
                    desiredPrimitive.Tag = "BAc";
                    break;
                case PrimitiveType.Entity:
                    desiredPrimitive.Lookup = new DotsPrimitive(PrimitiveType.EntityLookup);
                    desiredPrimitive.Iteration = new DotsPrimitive(PrimitiveType.EntityTypeHandle);
                    desiredPrimitive.ChunkFromPrimaryDecay = new DotsPrimitive(PrimitiveType.EntityNativeArray);
                    desiredPrimitive.ReferenceFromPrimaryDecay = new DotsPrimitive(PrimitiveType.Entity);
                    desiredPrimitive.IsQueryComponent = false;
                    desiredPrimitive.Tag = "EAc";
                    break;
                case PrimitiveType.SharedComponent:
                    desiredPrimitive.Lookup = new DotsPrimitive(PrimitiveType.SharedComponentLookup);
                    desiredPrimitive.Iteration = new DotsPrimitive(PrimitiveType.SharedComponentTypeHandle);
                    desiredPrimitive.ChunkFromPrimaryDecay = new DotsPrimitive(PrimitiveType.SharedComponent);
                    desiredPrimitive.ReferenceFromPrimaryDecay = new DotsPrimitive(PrimitiveType.SharedComponent);
                    desiredPrimitive.RequiresEntityStorageInfoLookup = true;
                    desiredPrimitive.Tag = "ScAc";
                    break;
            }
            if (!DependentPrimitives.TryGetValue(bind.ComponentTypeName, out var depPrim))
            {
                depPrim.Primitive = desiredPrimitive;
                depPrim.Dependencies = dependency;
            }
            else
            {
                // Merge the dependency to add into the existing one.
                depPrim.Dependencies.Merge(dependency);

                // merge the desired primitive with the existing one.
                depPrim.Primitive = depPrim.Primitive.Merge(desiredPrimitive);
            }

            DependentPrimitives[bind.ComponentTypeName] = depPrim;
            return true;
        }

        public bool TryGetExistingFieldPrimary(AspectField field, out AspectField existingField)
        {
            existingField = default;
            if (DependentPrimitives.TryGetValue(field.TypeName, out var depPrim)
                && depPrim.Dependencies.Original.Primary?.AspectDefinition == field.AspectDefinition)
            {
                existingField = depPrim.Dependencies.Original.Primary;
                return true;
            }

            return false;
        }

        public bool TryGetExistingFieldSecondary(AspectField field, out AspectField existingField)
        {
            existingField = default;
            if (DependentPrimitives.TryGetValue(field.TypeName, out var depPrim)
                && depPrim.Dependencies.Original.Secondary?.AspectDefinition == field.AspectDefinition)
            {
                existingField = depPrim.Dependencies.Original.Secondary;
                return true;
            }

            return false;
        }

        static AliasedDependencies MakePrimaryDependency(AspectField dependentField) => new AliasedDependencies(FieldDependency.MakePrimary(dependentField));
        static AliasedDependencies MakeSecondaryDependency(AspectField dependentField) => new AliasedDependencies(FieldDependency.MakeSecondary(dependentField));

        #endregion

        #region code generation printing

        void PrintCompleteDependency(Printer printer, string typename, bool isReadOnly)
                => printer.PrintBeginLine(isReadOnly
                        ? "state.EntityManager.CompleteDependencyBeforeRO<"
                        : "state.EntityManager.CompleteDependencyBeforeRW<")
                    .Print(typename).PrintEndLine(">();");

        public Printer PrintCompleteDependency(Printer printer, bool callIsReadOnly)
        {
            foreach (var depPrim in DependentPrimitives.Values)
                if(depPrim.Primitive.IsQueryComponent)
                    PrintCompleteDependency(printer, depPrim.Primitive.Bind.ComponentTypeName, callIsReadOnly || depPrim.Primitive.Bind.IsReadOnly);
            return printer;
        }

        public Printer LookupDeclare(Printer printer)
        {
            if (!m_EntityStorageInfoLookup.IsNothing)
                m_EntityStorageInfoLookup.Declare(printer, "_m_Esil", Bind: default);

            foreach (var depPrim in DependentPrimitives.Values)
                if (depPrim.Primitive.HasAnyDecaySequences)
                    depPrim.Primitive.Lookup.Declare(printer, depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);

            return printer;
        }

        public Printer LookupConstructFromState(Printer printer)
        {
            if (!m_EntityStorageInfoLookup.IsNothing)
                m_EntityStorageInfoLookup.ConstructFromState(printer, "_m_Esil", Bind: default);

            foreach (var depPrim in DependentPrimitives.Values)
                if (depPrim.Primitive.HasAnyDecaySequences)
                    depPrim.Primitive.Lookup.ConstructFromState(printer, depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);

            return printer;
        }

        public Printer LookupUpdate(Printer printer)
        {
            if (!m_EntityStorageInfoLookup.IsNothing)
                m_EntityStorageInfoLookup.Update(printer, "_m_Esil", Bind: default);

            foreach (var depPrim in DependentPrimitives.Values)
                if (depPrim.Primitive.HasAnyDecaySequences)
                    depPrim.Primitive.Lookup.Update(printer, depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);
            return printer;
        }

        public Printer LookupDecay(Printer printer, string aspectName)
        {
            if (!m_EntityStorageInfoLookup.IsNothing)
                printer.PrintBeginLine("var chunk = ").PrintWith(
                    m_EntityStorageInfoLookup.PrimaryDecay(printer, "_m_Esil", Bind: default)
                ).PrintEndLine(";");

            printer.PrintBeginLine("return new ").Print(aspectName).Print("(");
            var list = printer.AsListPrinter(", ").AsMultilineIndented;
            foreach (var depPrim in DependentPrimitives.Values)
            {
                if (depPrim.Primitive.HasPrimaryDecaySequence)
                    depPrim.Primitive.Lookup.PrimaryDecay(list.NextItemPrinter(), depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);
                if (depPrim.Primitive.HasSecondaryDecaySequence)
                    depPrim.Primitive.Lookup.SecondaryDecay(list.NextItemPrinter(), depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);
            }
            printer.PrintEndLine(");");
            return printer;
        }

        public Printer TypeHandleDeclare(Printer printer)
        {
            foreach (var depPrim in DependentPrimitives.Values)
                if(depPrim.Primitive.HasAnyDecaySequences)
                    depPrim.Primitive.Iteration.Declare(printer, depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);
            return printer;
        }
        public Printer TypeHandleConstructFromState(Printer printer)
        {
            foreach (var depPrim in DependentPrimitives.Values)
                if (depPrim.Primitive.HasAnyDecaySequences)
                    depPrim.Primitive.Iteration.ConstructFromState(printer, depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);
            return printer;
        }

        public Printer TypeHandleUpdate(Printer printer)
        {
            foreach (var depPrim in DependentPrimitives.Values)
                if (depPrim.Primitive.HasAnyDecaySequences)
                    depPrim.Primitive.Iteration.Update(printer, depPrim.PrimitiveFieldName, depPrim.Primitive.Bind);
            return printer;
        }

        public Printer TypeHandleDecay(Printer printer)
        {
            foreach (var depPrim in DependentPrimitives.Values)
            {
                if (depPrim.Primitive.HasPrimaryDecaySequence)
                    printer.PrintBeginLine($"resolved.")
                        .Print(depPrim.Dependencies.Original.Primary.InternalFieldName + depPrim.Primitive.ChunkFromPrimaryDecay.Tag).Print(" = ")
                        .PrintWith(depPrim.Primitive.Iteration.PrimaryDecay(printer, depPrim.PrimitiveFieldName,
                            depPrim.Primitive.Bind)).PrintEndLine(";");
                if (depPrim.Primitive.HasSecondaryDecaySequence)
                    printer.PrintBeginLine($"resolved.")
                        .Print(depPrim.Dependencies.Original.Secondary.InternalFieldName + depPrim.Primitive.ChunkFromSecondaryDecay.Tag).Print(" = ")
                        .PrintWith(depPrim.Primitive.Iteration.SecondaryDecay(printer, depPrim.PrimitiveFieldName,
                            depPrim.Primitive.Bind)).PrintEndLine(";");
            }
            return printer;
        }

        public Printer ChunkDeclare(Printer printer)
        {
            foreach (var depPrim in DependentPrimitives.Values)
            {
                if (depPrim.Primitive.HasPrimaryDecaySequence)
                {
                    printer.PrintLine("/// <summary>");
                    printer.PrintLine($"/// Chunk data for aspect field '{depPrim.Dependencies.Original.Primary.NestedName}'");
                    printer.PrintLine("/// </summary>");
                    depPrim.Primitive.ChunkFromPrimaryDecay.Declare(printer,
                        depPrim.Dependencies.Original.Primary.InternalFieldName +
                        depPrim.Primitive.ChunkFromPrimaryDecay.Tag, depPrim.Primitive.Bind);
                }

                if (depPrim.Primitive.HasSecondaryDecaySequence)
                {
                    printer.PrintLine("/// <summary>");
                    printer.PrintLine($"/// Chunk data for aspect field '{depPrim.Dependencies.Original.Secondary.NestedName}'");
                    printer.PrintLine("/// </summary>");
                    depPrim.Primitive.ChunkFromSecondaryDecay.Declare(printer,
                        depPrim.Dependencies.Original.Secondary.InternalFieldName +
                        depPrim.Primitive.ChunkFromSecondaryDecay.Tag, depPrim.Primitive.Bind);
                }
            }

            return printer;
        }

        public Printer ChunkDecay(Printer printer)
        {
            var listPrinter = printer.AsListPrinter(",").AsMultiline;
            foreach (var depPrim in DependentPrimitives.Values)
            {
                if (depPrim.Primitive.HasPrimaryDecaySequence)
                    depPrim.Primitive.ChunkFromPrimaryDecay.Decay(listPrinter.NextItemPrinter(),
                        depPrim.Dependencies.Original.Primary.InternalFieldName + depPrim.Primitive.ChunkFromPrimaryDecay.Tag, depPrim.Primitive.Bind);

                if (depPrim.Primitive.HasSecondaryDecaySequence)
                    depPrim.Primitive.ChunkFromSecondaryDecay.Decay(listPrinter.NextItemPrinter(), depPrim.Dependencies.Original.Secondary.InternalFieldName + depPrim.Primitive.ChunkFromSecondaryDecay.Tag, depPrim.Primitive.Bind);
            }

            return printer;
        }
        #endregion

        /// <summary>
        /// Check for any additional primitives needed to be declared
        /// </summary>
        public void ResolveDependencies()
        {
            // Look through all the primitives for any that requires a StorageInfoLookup to be declared.
            if (m_EntityStorageInfoLookup.IsNothing && DependentPrimitives.Values.Any(x => x.Primitive.RequiresEntityStorageInfoLookup))
                m_EntityStorageInfoLookup = new DotsPrimitive(PrimitiveType.EntityStorageInfoLookup);

        }

        /// <summary>
        /// Merge other PrimitivesRouter into this one.
        /// Happens when nested aspects are merged into the enclosing aspect
        /// </summary>
        /// <param name="other"></param>
        public void Merge(PrimitivesRouter other, PrimitiveBinding bindingOverride)
        {
            foreach (var depPrim in other.DependentPrimitives.Values)
            {
                if (!depPrim.Primitive.ReferenceFromPrimaryDecay.IsNothing)
                    AddDependency(depPrim.Primitive.ReferenceFromPrimaryDecay.Type, PrimitiveBinding.Override(depPrim.Primitive.Bind, bindingOverride), depPrim.Dependencies);
                if (!depPrim.Primitive.ReferenceFromSecondaryDecay.IsNothing)
                    AddDependency(depPrim.Primitive.ReferenceFromSecondaryDecay.Type, PrimitiveBinding.Override(depPrim.Primitive.Bind, bindingOverride), depPrim.Dependencies);
            }

            // keep m_EntityStorageInfoLookup if set or take other
            m_EntityStorageInfoLookup = m_EntityStorageInfoLookup.IsNothing
                ? other.m_EntityStorageInfoLookup
                : m_EntityStorageInfoLookup;
        }

        /// <summary>
        /// Print human readable state of this router.
        /// Used for debug
        /// </summary>
        /// <param name="printer"></param>
        public void Print(Printer printer)
        {
            var scope = printer.ScopePrinter("PrimitivesByTypeName {");
            {
                var list = scope.PrintBeginLine().AsListPrinter(", ").AsMultiline;
                list.NextItemPrinter().Debug.PrintKeyList(".DependentPrimitives", DependentPrimitives);
                list.NextItemPrinter().Debug.PrintKeyValue(".m_EntityStorageInfoLookup", m_EntityStorageInfoLookup);
                scope.PrintEndLine();
            }
            printer.CloseScope(scope, "}");
        }
    }
}
