namespace Unity.Entities.SourceGen.Aspect
{
    /// <summary>
    /// 2 dependencies of the same type may merge together with
    /// different outcomes.
    /// </summary>
    public enum MergePotential
    {
        Full,       // the 2 dependencies may be fully merged
        Partial,    // only a part of the second dependency will be transferred to the first dependency
        Impossible  // nothing from the second dependency can be transferred to the first dependency
    }

    /// <summary>
    /// A maximum of 2 AspectField may depend on the primitive.
    /// The primary field is the declaring field.
    /// 
    /// The AspectField of this FieldDependency correspond to the Primary and Secondary decay of the primitive
    /// the fields depend on.
    /// Primitives like ComponentLookup and ComponentTypeHandle have 2 decays : one for Ref and one for EnabledRef.
    /// In that case, the Primary field is the Ref and the Secondary field is the EnabledRef.
    /// </summary>
    public struct FieldDependency : IPrintable
    {
        public AspectField Primary;
        public AspectField Secondary;

        /// <summary>
        /// Some primitives types like DynamicBuffer and Entity only have 1 dependent field.
        /// </summary>
        public AspectField Field
        {
            get => Primary;
            set => Primary = Field;
        }
        public AspectField DependentField => Primary ?? Secondary;
        public static FieldDependency MakePrimary(AspectField child)
            => new FieldDependency
            {
                Primary = child,
                Secondary = default
            };
        public static FieldDependency MakeSecondary(AspectField child)
            => new FieldDependency
            {
                Primary = default,
                Secondary = child
            };

        public AspectField DeclaringField => Primary ?? Secondary;
        
        static bool CanMerge(AspectField a, AspectField b)
            => a == null || b == null || a == b;

        public MergePotential MergePotentialWith(FieldDependency dependency)
        {
            var canMergePrimary = CanMerge(Primary, dependency.Primary);
            var canMergeSecondary = CanMerge(Secondary, dependency.Secondary);
            if (canMergePrimary && canMergeSecondary) return MergePotential.Full;
            if (canMergePrimary || canMergeSecondary) return MergePotential.Partial;
            return MergePotential.Impossible;
        }

        public FieldDependency Merge(FieldDependency dependency)
        {
            System.Diagnostics.Debug.Assert(MergePotentialWith(dependency) != MergePotential.Impossible);
            if (Primary == null)
            {
                Primary = dependency.Primary;
                dependency.Primary = null;
            }
            if (Secondary == null)
            {
                Secondary = dependency.Secondary;
                dependency.Secondary = null;
            }
            return dependency;
        }

        public void Print(Printer printer)
        {
            var scope = printer.ScopePrinter("BinaryDependency {");
            var children = scope.PrintBeginLine().AsListPrinter(",").AsMultiline;
            {
                children.NextItemPrinter().Debug.PrintKeyValueNullable(".Primary", Primary);
                children.NextItemPrinter().Debug.PrintKeyValueNullable(".Secondary", Secondary);
            }
            scope.PrintEndLine();
            printer.CloseScope(scope, "}");
        }
    }
}
