using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

public static class JobEntityGeneratorErrors
{
    const string k_ErrorTitle = "IJobEntity Error";

    public static void SGJE0003(
        ISourceGeneratorDiagnosable context,
        Location location,
        string parameterName,
        string parameterType)
    {
        context.LogError(
            nameof(SGJE0003),
            k_ErrorTitle,
            $"The parameter '{parameterName}' of type {parameterType} will be ignored.",
            location);
    }

    public static void SGJE0006(ISourceGeneratorDiagnosable context, Location location, string jobEntityTypeName, string methodSignature, string nonIntegerEntityQueryParameter, string attributeName)
    {
        context.LogError(
            nameof(SGJE0006),
            k_ErrorTitle,
            $"{jobEntityTypeName}.{methodSignature} accepts a non-integer parameter ('{nonIntegerEntityQueryParameter}') with the [{attributeName}] attribute. " +
            $"This is not allowed. The [{attributeName}] attribute may only be applied on integer parameters.",
            location);
    }

    public static void SGJE0007(ISourceGeneratorDiagnosable context, Location location, string jobEntityTypeName, string methodSignature, string attributeName)
    {
        context.LogError(
            nameof(SGJE0007),
            k_ErrorTitle,
            $"{jobEntityTypeName}.{methodSignature} accepts more than one integer parameters with the [{attributeName}] attribute. " +
            $"This is not allowed. The [{attributeName}] attribute can only be applied EXACTLY ONCE on an integer parameter in {jobEntityTypeName}.{methodSignature}.",
            location);
    }

    public static void SGJE0008(ISourceGeneratorDiagnosable context, Location location, string jobEntityTypeName, List<IMethodSymbol> userDefinedExecuteMethods)
    {
        context.LogError(nameof(SGJE0008), k_ErrorTitle,
            $"You have defined {userDefinedExecuteMethods.Count} Execute() method(s) in {jobEntityTypeName}. "
            + "Please define exactly one Execute() method in each IJobEntity type. "
            + $"List of perpetrators: {userDefinedExecuteMethods.Select(method => method.ToString()).SeparateByCommaAndSpace()}. "
            //+$"With Attributes: {userDefinedExecuteMethods.Select(m => m.GetAttributes().Select(a => a.AttributeClass.ToFullName()).SeparateByComma()).SeparateByComma()}"
            , location);
    }

    // TODO: Needs test
    public static void SGJE0010(ISourceGeneratorDiagnosable context, Location location, string parameter, string parameterType)
    {
        context.LogError(
            nameof(SGJE0010),
            k_ErrorTitle,
            $"IJobEntity.Execute() parameter '{parameter}' of type {parameterType} is not supported.",
            location);
    }

    // TODO: Needs test
    public static void SGJE0011(ISourceGeneratorDiagnosable diagnosable, Location location, string notValidParam)
    {
        diagnosable.LogError(
            nameof(SGJE0011),
            k_ErrorTitle,
            $"Execute() parameter '{notValidParam}' is not a supported parameter in an IJobEntity type.",
            location);
    }

    // TODO: Needs test
    public static void SGJE0012(ISourceGeneratorDiagnosable diagnosable, Location location, string parameterType)
    {
        diagnosable.LogError(
            nameof(SGJE0012),
            k_ErrorTitle,
            $"{parameterType} implements IBufferElementData and must be used as DynamicBuffer<{parameterType}>.",
            location);
    }

    public static void SGJE0013(ISourceGeneratorDiagnosable diagnosable, Location location, string name, string parameterType)
    {
        diagnosable.LogError(
            nameof(SGJE0013),
            k_ErrorTitle,
            $"{name} has a shared component, {parameterType}, that uses the Ref keyword, which is not safe for shared components, instead pass by value or use the 'in' keyword.",
            location);
    }

    public static void SGJE0016(ISourceGeneratorDiagnosable diagnosable, Location location, string name, string parameterType)
    {
        diagnosable.LogError(
            nameof(SGJE0016),
            k_ErrorTitle,
            $"`{name}` has an empty component of type `{parameterType}`. Empty components must not be used with the `ref` keyword.",
            location);
    }

    public static void SGJE0017(ISourceGeneratorDiagnosable diagnosable, Location location, string jobName, string typeName)
    {
        diagnosable.LogError(
            nameof(SGJE0017),
            k_ErrorTitle,
            $"{jobName} has duplicate components of same type {typeName}. Remove all but one to fix.",
            location);
    }

    public static void SGJE0018(ISourceGeneratorDiagnosable diagnosable, Location location)
    {
        diagnosable.LogError(
            nameof(SGJE0018),
            k_ErrorTitle,
            "`RefRW<T>`, `RefRO<T>`, `EnabledRefRW<T>`, and `EnabledRefRO<T>` may not be used with `ref` or `in`.",
            location);
    }

    public static void SGJE0019(ISourceGeneratorDiagnosable diagnosable, Location location, string typeFullName)
    {
        diagnosable.LogError(
            nameof(SGJE0019),
            k_ErrorTitle,
            $"`{typeFullName}` does not implement `IComponentData`, and thus cannot be used with `EnabledRefRW<T>` or `EnabledRefRO<T>`.",
            location);
    }

    public static void SGJE0020(ISourceGeneratorDiagnosable diagnosable, Location location, string jobName)
    {
        diagnosable.LogError(
            nameof(SGJE0020),
            k_ErrorTitle,
            $"`{jobName}` is an IJobEntity job that uses a generic type parameter.  This is not currently supported.  Please use IJobChunk if you need support for generic parameters.",
            location);
    }

    public static void SGJE0021(ISourceGeneratorDiagnosable diagnosable, Location location, string parameterName)
    {
        diagnosable.LogError(
            nameof(SGJE0021),
            k_ErrorTitle,
            $"{parameterName} is an Aspect passed with a `ref` or `in` keyword.  Aspects are already act as reference types and should just be passed in by value.",
            location);
    }

    public static void SGJE0022(ISourceGeneratorDiagnosable diagnosable, Location location, string parameterName)
    {
        diagnosable.LogError(
            nameof(SGJE0022),
            k_ErrorTitle,
            $"IJobEntity has a Managed component {parameterName} that is used with a `ref` or `in` modifier.  Managed components are already passed by reference and are do not have readonly protection.  Please pass directly instead, without the `ref` or `in` modifier.",
            location);
    }

    public static void SGJE0023(ISourceGeneratorDiagnosable diagnosable, Location location, string parameterTypeFullName, string parameterAccessibility, string jobEntityTypeFullName, string jobAccessibility)
    {
        diagnosable.LogError(
            nameof(SGJE0023),
            k_ErrorTitle,
            "All parameter types in `IJobEntity.Execute()` methods must be as accessible as *or* more accessible than the the `IJobEntity` types in which they are used. " +
            $"`{parameterTypeFullName}` is {parameterAccessibility}, but is used as a parameter type in the `Execute()` method of `{jobEntityTypeFullName}`, which is {jobAccessibility}. This is not allowed.",
            location);
    }
}
