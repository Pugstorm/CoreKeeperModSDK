using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

struct SharedComponentFilterInfo
{
    public ITypeSymbol TypeSymbol { get; set; }
    public ArgumentSyntax Argument { get; set; }
    public bool IsManaged { get; set; }
}

struct IfeType
{
    internal IReadOnlyCollection<ReturnedTupleElementDuringEnumeration> ReturnedTupleElementsDuringEnumeration
    {
        get;
        set;
    }

    public string TypeName { get; set; }
    public string FullyQualifiedTypeName { get; set; }
    public bool MustReturnEntityDuringIteration { get; set; }
    public AttributeData BurstCompileAttribute { get; set; }
    public bool PerformsCollectionChecks { get; set; }

    public bool UseBurst => BurstCompileAttribute != null;

    public (string FullName, string Creation) ResultType(IEnumerable<string> queryResultConstructorArgs)
    {
        string queryReturnTypeFullName;

        if (MustReturnEntityDuringIteration)
        {
            var typeParameterFullNames = ReturnedTupleElementsDuringEnumeration.Select(f => f.TypeSymbolFullName).SeparateByCommaAndSpace();
            queryReturnTypeFullName = $"Unity.Entities.QueryEnumerableWithEntity<{typeParameterFullNames}>";
            return
            (
                queryReturnTypeFullName,
                $"new {queryReturnTypeFullName}({queryResultConstructorArgs.SeparateByComma()})"
            );
        }

        if (ReturnedTupleElementsDuringEnumeration.Count > 1)
        {
            queryReturnTypeFullName =
                $"({ReturnedTupleElementsDuringEnumeration.Select(fieldInfo => fieldInfo.TypeSymbolFullName).SeparateByCommaAndSpace()})";

            return (queryReturnTypeFullName, $"({queryResultConstructorArgs.SeparateByComma()})");
        }

        return (ReturnedTupleElementsDuringEnumeration.Single().TypeSymbolFullName, queryResultConstructorArgs.Single());
    }
}
