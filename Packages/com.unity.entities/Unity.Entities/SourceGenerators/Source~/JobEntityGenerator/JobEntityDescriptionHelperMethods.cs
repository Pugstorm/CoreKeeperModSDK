using System;
using Microsoft.CodeAnalysis;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

public partial class JobEntityDescription
{
    private static bool IsLessAccessibleThan(ISymbol executeParameterSymbol, ISymbol jobEntityTypeSymbol)
    {
        var jobEntityAccessibility = jobEntityTypeSymbol.DeclaredAccessibility;
        var jobEntityContainingType = jobEntityTypeSymbol.ContainingType;

        while (jobEntityContainingType != null)
        {
            // E.g. if an `IJobEntity` type is declared `public` but nested within a non-public type, then its accessibility is in fact less accessible than `public`
            if (IsLessAccessibleThan(jobEntityContainingType.DeclaredAccessibility, jobEntityAccessibility))
                jobEntityAccessibility = jobEntityContainingType.DeclaredAccessibility;

            jobEntityContainingType = jobEntityContainingType.ContainingType;
        }

        var executeParameterSymbolAccessibility = executeParameterSymbol.DeclaredAccessibility;
        var parameterSymbolContainingType = executeParameterSymbol.ContainingType;

        while (parameterSymbolContainingType != null)
        {
            // E.g. if an `IComponentData` type is declared `public` but nested within a non-public type, then its accessibility is in fact less accessible than `public`
            if (IsLessAccessibleThan(parameterSymbolContainingType.DeclaredAccessibility, executeParameterSymbolAccessibility))
                executeParameterSymbolAccessibility = parameterSymbolContainingType.DeclaredAccessibility;

            parameterSymbolContainingType = parameterSymbolContainingType.ContainingType;
        }

        return IsLessAccessibleThan(executeParameterSymbolAccessibility, jobEntityAccessibility);
    }

    private static bool IsLessAccessibleThan(Accessibility accessibility1, Accessibility accessibility2)
    {
        return accessibility1 switch
        {
            Accessibility.Private => accessibility2 != Accessibility.Private,
            Accessibility.Internal => accessibility2 == Accessibility.ProtectedOrInternal ||
                                      accessibility2 == Accessibility.Protected ||
                                      accessibility2 == Accessibility.Public,
            Accessibility.Protected => accessibility2 == Accessibility.ProtectedOrInternal ||
                                       accessibility2 == Accessibility.Internal ||
                                       accessibility2 == Accessibility.Public,
            Accessibility.ProtectedOrInternal => accessibility2 == Accessibility.Public,
            Accessibility.Public => false,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
