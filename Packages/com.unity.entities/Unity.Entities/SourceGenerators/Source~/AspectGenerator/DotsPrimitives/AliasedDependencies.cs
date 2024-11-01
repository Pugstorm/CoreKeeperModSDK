using System.Collections.Generic;

namespace Unity.Entities.SourceGen.Aspect
{

    /// <summary>
    /// Holds a FieldDependency called Original.
    /// When merging this dependency, the second AliasedDependencies.Original will
    /// become an alias in the AliasedDependencies it is merging into.
    /// </summary>
    public struct AliasedDependencies : IPrintable
    {
        /// <summary>
        /// Will contain the first dependent field found requiring the primitive
        /// </summary>
        public FieldDependency Original;

        /// <summary>
        /// Will contain all additional field that also require the same primitive
        /// </summary>
        public List<FieldDependency> Aliases;

        List<FieldDependency> GetOrCreateAliases() => Aliases ??= new List<FieldDependency>();

        public AspectField DeclaringField => Original.DeclaringField;

        public IEnumerable<FieldDependency> AllSubDependency
        {
            get
            {
                yield return Original;
                if (Aliases != null)
                    foreach (var a in Aliases)
                        yield return a;
            }
        }

        public AliasedDependencies(FieldDependency original)
        {
            Original = original;
            Aliases = default;
        }

        public void AddAlias(FieldDependency dependency) => GetOrCreateAliases().Add(dependency);
        
        public MergePotential MergePotentialWith(AliasedDependencies dependency) => MergePotential.Full;
        public AliasedDependencies Merge(AliasedDependencies dependency)
        {
            switch (Original.MergePotentialWith(dependency.Original))
            {
                case MergePotential.Full:
                    Original.Merge(dependency.Original);
                    return default;
                case MergePotential.Partial:
                    AddAlias(Original.Merge(dependency.Original));
                    break;
                case MergePotential.Impossible:
                    AddAlias(dependency.Original);
                    break;
            }

            if (dependency.Aliases != null)
            {
                System.Diagnostics.Debug.Assert(MergePotentialWith(dependency) != MergePotential.Impossible);
                if (Aliases != null)
                    Aliases.AddRange(dependency.Aliases);
                else
                    Aliases = new List<FieldDependency>(dependency.Aliases);
                dependency.Aliases = null;
                return dependency;
            }
            return dependency;
        }

        public void Print(Printer printer)
        {
            var scope = printer.ScopePrinter("AliasedDependencies {");
            {
                var list = scope.PrintBeginLine().AsListPrinter(", ").AsMultiline;
                list.NextItemPrinter().Debug.PrintKeyValue(".Original", Original);
                if (Aliases != null)
                {
                    var scopeAliases = printer.ScopePrinter(".Aliases {");
                    scopeAliases.PrintBeginLine().Debug.PrintListMultiline(", ", Aliases);
                    printer.CloseScope(scopeAliases, "}");
                }
                else
                    list.NextItemPrinter().Debug.PrintKeyValue(".Aliased", "<none>");
                scope.PrintEndLine();
            }
            printer.CloseScope(scope, "}");
        }
    }
}
