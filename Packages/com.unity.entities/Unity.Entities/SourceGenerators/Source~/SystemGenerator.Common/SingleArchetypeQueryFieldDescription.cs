using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common
{
    public readonly struct SingleArchetypeQueryFieldDescription : IEquatable<SingleArchetypeQueryFieldDescription>, IQueryFieldDescription
    {
        readonly Archetype _archetype;
        readonly IReadOnlyList<Query> _changeFilterTypes;
        readonly string _queryStorageFieldName;

        public string GetFieldDeclaration(string generatedQueryFieldName, bool forcePublic = false)
            => $"{(forcePublic ? "public " : "")}global::Unity.Entities.EntityQuery {generatedQueryFieldName};";

        public SingleArchetypeQueryFieldDescription(
            Archetype archetype,
            IReadOnlyList<Query> changeFilterTypes = null,
            string queryStorageFieldName = null)
        {
            _archetype = archetype;
            _changeFilterTypes = changeFilterTypes ?? Array.Empty<Query>();
            _queryStorageFieldName = queryStorageFieldName;
        }

        public bool Equals(SingleArchetypeQueryFieldDescription other)
        {
            return _archetype.Equals(other._archetype)
                   && _changeFilterTypes.SequenceEqual(other._changeFilterTypes)
                   && _queryStorageFieldName == other._queryStorageFieldName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (obj.GetType() != GetType())
                return false;
            return Equals((SingleArchetypeQueryFieldDescription)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 19;
                hash = hash * 31 + _archetype.GetHashCode();

                foreach (var changeFilterType in _changeFilterTypes)
                    hash = hash * 31 + changeFilterType.GetHashCode();

                return hash;
            }
        }
        public static bool operator ==(SingleArchetypeQueryFieldDescription left, SingleArchetypeQueryFieldDescription right) => Equals(left, right);
        public static bool operator !=(SingleArchetypeQueryFieldDescription left, SingleArchetypeQueryFieldDescription right) => !Equals(left, right);

        static (HashSet<string> readOnlyTypeNames, HashSet<string> readWriteTypeNames) GetDistinctRequiredTypeNames(IEnumerable<Query> presentTypes)
        {
            var readOnlyTypeNames = new HashSet<string>();
            var readWriteTypeNames = new HashSet<string>();

            foreach (var type in presentTypes)
                AddDistinctQueryType(type, type.IsReadOnly);

            return (readOnlyTypeNames, readWriteTypeNames);

            void AddDistinctQueryType(Query q, bool isReadOnly)
            {
                var queryTypeFullName = q.TypeSymbol.ToFullName();
                if (!isReadOnly)
                {
                    readOnlyTypeNames.Remove(queryTypeFullName);
                    readWriteTypeNames.Add(queryTypeFullName);
                }
                else if (!readWriteTypeNames.Contains(queryTypeFullName))
                    readOnlyTypeNames.Add(queryTypeFullName);
            }
        }

        public void WriteEntityQueryFieldAssignment(IndentedTextWriter writer, string generatedQueryFieldName)
        {
            if (_queryStorageFieldName != null)
                writer.WriteLine($"{_queryStorageFieldName} = ");

            var codeAspect = new List<string>(8);
            writer.WriteLine($"{generatedQueryFieldName} = ");
            writer.Indent++;
            writer.WriteLine("entityQueryBuilder");
            writer.Indent++;

            var requiredTypes = _archetype.All.Concat(_changeFilterTypes).Except(_archetype.Disabled);
            var presentComponentTypes = new List<Query>(capacity: 8);

            foreach (var comp in requiredTypes)
                if (comp.TypeSymbol.IsAspect())
                    codeAspect.Add($".WithAspect<{comp.TypeSymbol.ToFullName()}>()");
                else
                    presentComponentTypes.Add(comp);

            foreach (var comp in _archetype.Any)
            {
                writer.WriteLine(comp.IsReadOnly
                    ? $".WithAny<{comp.TypeSymbol.ToFullName()}>()"
                    : $".WithAnyRW<{comp.TypeSymbol.ToFullName()}>()");
            }
            foreach (var comp in _archetype.None)
            {
                writer.WriteLine(comp.IsReadOnly
                    ? $".WithNone<{comp.TypeSymbol.ToFullName()}>()"
                    // We support specifying an enableable type as a `None` component *and* as an iterable query type in the same `SystemAPI.Query` invocation.
                    // Users may write e.g. `SystemAPI.Query<EnabledRefRW<MyEnableableComponent>>().WithNone<MyEnableableComponent>()` in order to specifically iterate through
                    // *disabled* `MyEnableableComponent`s. In that case, `MyEnableableComponent` is labelled a `None` component with read-write access, since `EnabledRefRW`
                    // usages require read-write access. Since `.WithNoneRW` is not available as part of the `EntityQueryBuilder` public API, we can use `.WithDisabledRW` instead.
                    : $".WithDisabledRW<{comp.TypeSymbol.ToFullName()}>()");
            }
            foreach (var comp in _archetype.Disabled)
            {
                writer.WriteLine(comp.IsReadOnly
                    ? $".WithDisabled<{comp.TypeSymbol.ToFullName()}>()"
                    : $".WithDisabledRW<{comp.TypeSymbol.ToFullName()}>()");
            }
            foreach (var comp in _archetype.Present)
            {
                writer.WriteLine(comp.IsReadOnly
                    ? $".WithPresent<{comp.TypeSymbol.ToFullName()}>()"
                    : $".WithPresentRW<{comp.TypeSymbol.ToFullName()}>()");
            }
            foreach (var comp in _archetype.Absent)
                writer.WriteLine($".WithAbsent<{comp.TypeSymbol.ToFullName()}>()");

            var distinctPresentTypeNames = GetDistinctRequiredTypeNames(presentComponentTypes);
            foreach (var ro in distinctPresentTypeNames.readOnlyTypeNames)
                writer.WriteLine($".WithAll<{ro}>()");

            foreach (var ro in distinctPresentTypeNames.readWriteTypeNames)
                writer.WriteLine($".WithAllRW<{ro}>()");

            // Append all ".WithAspect" calls. They must be done after all "WithAll", "WithAny" and "WithNone" calls to avoid component aliasing
            foreach (var code in codeAspect)
                writer.WriteLine(code);

            if(_archetype.Options != EntityQueryOptions.Default)
                writer.WriteLine($".WithOptions({_archetype.Options.GetAsFlagStringSeperatedByOr()})");

            writer.WriteLine(".Build(ref state);");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("entityQueryBuilder.Reset();");

            if (_changeFilterTypes.Any())
            {
                writer.WriteLine($@"{generatedQueryFieldName}.SetChangedVersionFilter(new ComponentType[{_changeFilterTypes.Count}]");
                writer.WriteLine("{");
                writer.Indent++;

                for (var index = 0; index < _changeFilterTypes.Count; index++)
                {
                    writer.WriteLine($"new ComponentType(typeof({_changeFilterTypes[index].TypeSymbol.ToFullName()}))");

                    if (index < _changeFilterTypes.Count - 1)
                        writer.WriteLine(",");
                }

                writer.Indent--;
                writer.WriteLine("});");
            }
        }
    }
}
