#pragma warning disable 8509
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public static class LambdaJobsErrors
{
    const string k_ErrorTitle = "Lambda Jobs Error";

    public static void DC0003(SystemDescription systemDescription, Location location, string name)
    {
        systemDescription.LogError(nameof(DC0003), k_ErrorTitle,
            $"The name '{name}' is already used in this system.", location);
    }

    public static void DC0004(SystemDescription systemDescription, Location location, string variableName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0004), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} Lambda expression captures a non-value type '{variableName}'. "
            + "This is either due to use of a field, a member method, or property of the containing system, or the cause of explicit use of the this keyword. ".EmitIfTrue(variableName=="this")
            + "This is only allowed with .WithoutBurst() and .Run()", location);
    }

    public static void DC0005(SystemDescription systemDescription, Location location, string parameterName, string parameterTypeName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0005), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} Lambda expression parameter '{parameterName}' with type {parameterTypeName} is not supported", location);
    }

    public static void DC0008(SystemDescription systemDescription, Location location, string methodName)
    {
        systemDescription.LogError(nameof(DC0008), k_ErrorTitle,
            $"The argument to {methodName} needs to be a literal value.", location);
    }

    public static void DC0009(SystemDescription systemDescription, Location location, string methodName)
    {
        systemDescription.LogError(nameof(DC0009), k_ErrorTitle,
            $"{methodName} is only allowed to be called once.", location);
    }

    public static void DC0010(SystemDescription systemDescription, Location location, string methodName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0010), k_ErrorTitle,
            $"The {lambdaJobKind.ToName()} statement contains dynamic code in {methodName} that cannot be statically analyzed.", location);
    }

    public static void DC0012(SystemDescription systemDescription, Location location, string argumentName, string constructionMethodName)
    {
        systemDescription.LogError(nameof(DC0012), k_ErrorTitle,
            $"Entities.{constructionMethodName} is called with an invalid argument {argumentName}. You can only use Entities.{constructionMethodName} on local variables that are captured inside the lambda. Please assign the field to a local variable and use that instead.", location);
    }

    public static void DC0013(SystemDescription systemDescription, Location location, string capturedVariableName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0013), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} Lambda expression writes to captured variable '{capturedVariableName}' that is then read outside. This is only supported when you use .Run().", location);
    }

    public static void DC0014(SystemDescription systemDescription, Location location, string parameterName, string[] supportedParameters)
    {
        systemDescription.LogError(nameof(DC0014), k_ErrorTitle,
            $"Execute() parameter '{parameterName}' is not a supported parameter in an IJobEntitiesForEach type. Supported `int` parameter names are {supportedParameters.SeparateByComma()}.", location);
    }

    public static void DC0223(SystemDescription systemDescription, Location location, string typeName, bool isManagedIComponentData, LambdaJobKind lambdaJobKind)
    {
        var componentText = isManagedIComponentData ? $"managed IComponentData `{typeName}`" : $"ISharedComponentData type {typeName}";
        var message = $"{lambdaJobKind.ToName()} uses {componentText}. This is only supported when using .WithoutBurst() and .Run().";
        systemDescription.LogError(nameof(DC0223), k_ErrorTitle, message, location);
    }

    public static void DC0020(SystemDescription systemDescription, Location location, string sharedComponentTypeName)
    {
        systemDescription.LogError(nameof(DC0020), k_ErrorTitle,
            $"ISharedComponentData type {sharedComponentTypeName} can not be received by ref. Use by value or in.", location);
    }

    public static void DC0021(SystemDescription systemDescription, Location location, string parameterName, string unsupportedTypeName)
    {
        systemDescription.LogError(nameof(DC0021), k_ErrorTitle,
            $"parameter '{parameterName}' has type {unsupportedTypeName}. This type is not a IComponentData / ISharedComponentData and is therefore not a supported parameter type for Entities.ForEach.", location);
    }

    public static void DC0024(SystemDescription systemDescription, Location location, string componentTypeName)
    {
        systemDescription.LogError(nameof(DC0024), k_ErrorTitle,
            $"Entities.ForEach uses managed IComponentData `{componentTypeName}` by ref. To get write access, receive it without the ref modifier.", location);
    }

    public static void DC0025(SystemDescription systemDescription, Location location, string typeName)
    {
        systemDescription.LogError(nameof(DC0025), k_ErrorTitle,
            $"Type `{typeName}` is not allowed to implement method `OnCreateForCompiler'.  This method is supplied by code generation.", location);
    }

    public static void DC0027(SystemDescription systemDescription, Location location, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0027), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} Lambda expression makes a structural change. Use an EntityCommandBuffer to make structural changes or add a .WithStructuralChanges invocation to the {lambdaJobKind.ToName()} to allow for structural changes.  Note: WithStructuralChanges is runs without burst and is only allowed with .Run().", location);
    }

    public static void DC0029(SystemDescription systemDescription, Location location, LambdaJobKind lambdaJobKind)
    {
        var lambdaJobKindName = lambdaJobKind.ToName();
        systemDescription.LogError(nameof(DC0029), k_ErrorTitle, $"{lambdaJobKindName} Lambda expression has a nested {lambdaJobKindName} Lambda expression. Only a single {lambdaJobKindName} Lambda expression is currently supported.", location);
    }

    public static void DC0031(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(nameof(DC0031), k_ErrorTitle,
            $"Entities.ForEach Lambda expression stores the EntityQuery with a .WithStoreEntityQueryInField invocation but does not store it in a valid field.  Entity Queries can only be stored in fields of the containing SystemBase.", location);
    }

    public static void DC0033(SystemDescription systemDescription, Location location, string parameterName, string unsupportedTypeName)
    {
        systemDescription.LogError(nameof(DC0033), k_ErrorTitle,
            $"{unsupportedTypeName} implements IBufferElementData and must be used as DynamicBuffer<{unsupportedTypeName}>. Parameter '{parameterName}' is not a IComponentData / ISharedComponentData and is therefore not a supported parameter type for Entities.ForEach.", location);
    }

    public static void DC0034(SystemDescription systemDescription, Location location, string argumentName, string unsupportedTypeName, string constructionMethodName)
    {
        systemDescription.LogError(nameof(DC0034), k_ErrorTitle,
            $"Entities.{constructionMethodName} is called with an argument {argumentName} of unsupported type {unsupportedTypeName}. It can only be called with an argument that is marked with [NativeContainerAttribute] or a type that has a field marked with [NativeContainerAttribute].", location);
    }

    public static void DC0035(SystemDescription systemDescription, Location location, string argumentName, string constructionMethodName)
    {
        systemDescription.LogError(nameof(DC0035), k_ErrorTitle,
            $"Entities.{constructionMethodName} is called with argument {argumentName}, but that value is not used in the lambda function.", location);
    }

    public static void DC0043(SystemDescription systemDescription, Location location, string jobName)
    {
        systemDescription.LogError(nameof(DC0043), k_ErrorTitle,
            $"Entities.WithName cannot be used with name '{jobName}'. The given name must consist of letters, digits, and underscores only, and may not contain two consecutive underscores.", location);
    }

    public static void DC0044(SystemDescription systemDescription, Location location, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0044), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} can only be used with an inline lambda.  Calling it with a delegate stored in a variable, field, or returned from a method is not supported.", location);
    }

    public static void DC0046(SystemDescription systemDescription, Location location, string methodName, string typeName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0046), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} cannot use component access method {methodName} that needs write access with the same type {typeName} that is used in lambda parameters.", location);
    }

    public static void DC0047(SystemDescription systemDescription, Location location, string methodName, string typeName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0047), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} cannot use component access method {methodName} with the same type {typeName} that is used in lambda parameters with write access (as ref).", location);
    }

    public static void DC0050(SystemDescription systemDescription, Location location, string parameterTypeFullName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0050), k_ErrorTitle,
            $"Type {parameterTypeFullName} cannot be used as an {lambdaJobKind.ToName()} parameter as generic types and generic parameters are not currently supported in {lambdaJobKind.ToName()}", location);
    }

    public static void DC0053(SystemDescription systemDescription, Location location, string systemTypeName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0053), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} cannot be used in system {systemTypeName} as {lambdaJobKind.ToName()} in generic system types are not supported.", location);
    }

    public static void DC0054(SystemDescription systemDescription, Location location, string methodName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0054), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} is used in generic method {methodName}.  This is not currently supported.", location);
    }

    public static void DC0055(SystemDescription systemDescription, Location location, string lambdaParameterComponentTypeName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogWarning(nameof(DC0055), k_ErrorTitle,
            $"{lambdaJobKind.ToName()} passes {lambdaParameterComponentTypeName} by value.  Any changes made will not be stored to the underlying component.  Please specify the access you require. Use 'in' for read-only access or `ref` for read-write access.", location);
    }

    public static void DC0057(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(nameof(DC0057), k_ErrorTitle,
            "WithStructuralChanges cannot be used with Job.WithCode.  WithStructuralChanges should instead be used with Entities.ForEach.", location);
    }

    public static void DC0059(SystemDescription systemDescription, Location location, string methodName, LambdaJobKind lambdaJobKind)
    {
        systemDescription.LogError(nameof(DC0059), k_ErrorTitle,
            $"The argument indicating read only access to {methodName} cannot be dynamic and must to be a boolean literal `true` or `false` value when used inside of an {lambdaJobKind.ToName()}.", location);
    }

    public static void DC0070(SystemDescription systemDescription, Location location, ITypeSymbol duplicateType)
    {
        systemDescription.LogError(
            nameof(DC0070), k_ErrorTitle,
            $"{duplicateType.Name} is used multiple times as a lambda parameter. Each IComponentData, ISharedComponentData, DynamicBuffer<T> type may only be used once in Entities.ForEach().", location);
    }

    public static void DC0073(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(nameof(DC0073),
            k_ErrorTitle,
            "WithScheduleGranularity cannot be used with Schedule or Run as it controls how parallel job scheduling occurs. Use ScheduleParallel with this feature.", location);
    }

    public static void DC0074(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(
            nameof(DC0074),
            k_ErrorTitle,
            "When using an EntityCommandBuffer parameter to an Entities.ForEach() expression, you must do exactly one of the following: " +
            "1. Use .WithDeferredPlaybackSystem() with .Run(), .Schedule() or .ScheduleParallel() to play back entity commands when the EntityCommandBufferSystem runs. " +
            "2. Use .WithImmediatePlayback() with .Run() to play commands back immediately after this Entities.ForEach() runs. ",
            location);
    }

    public static void DC0075(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(
            nameof(DC0075),
            k_ErrorTitle,
            "You have invoked both .WithDeferredPlaybackSystem<T>() and .WithImmediatePlayback() in the same Entities.ForEach() expression. " +
            "Allowed usages are: " +
            "1. If you are using an EntityCommandBuffer parameter, specify either .WithDeferredPlaybackSystem<T>() or .WithImmediatePlayback(); " +
            "2. If not, use neither.",
            location);
    }

    public static void DC0076(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(
            nameof(DC0076),
            k_ErrorTitle,
            "You have specified more than one EntityCommandBuffer parameter in the same Entities.ForEach() expression. This is not allowed. " +
            "Please specify at most one EntityCommandBuffer parameter in each Entities.ForEach().",
            location);
    }

    public static void DC0077(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(
            nameof(DC0077),
            k_ErrorTitle,
            "You invoked .WithImmediatePlayback() together with .Schedule()/.ScheduleParallel(). This is not allowed. " +
            ".WithImmediatePlayback() may only be used with .Run().",
            location);
    }

    public static void DC0078(SystemDescription systemDescription, Location location)
    {
        systemDescription.LogError(
            nameof(DC0078),
            k_ErrorTitle,
            "You invoked .WithDeferredPlaybackSystem<T>() multiple times. Please invoke it EXACTLY once to specify EXACTLY one system for playing back commands.",
            location);
    }

    public static void DC0079(SystemDescription systemDescription, string methodSignature, Location location)
    {
        systemDescription.LogError(
            nameof(DC0079),
            k_ErrorTitle,
            "The only EntityCommandBuffer methods you may use inside of Entities.ForEach() are those which are marked with the [SupportedInEntitiesForEach] attribute. " +
            $"You attempted to invoke {methodSignature}, whose usage is not supported inside of Entities.ForEach().",
            location);
    }

    public static void DC0080(SystemDescription systemDescription, string methodSignature, Location location)
    {
        systemDescription.LogError(
            nameof(DC0080),
            k_ErrorTitle,
            "The following EntityCommandBuffer methods are supported ONLY on the main thread: 1) Those with an EntityQuery parameter, and 2) those accepting managed IComponentData or IEnableableComponent types. " +
            $"This means you may only use {methodSignature} inside of Entities.ForEach() when calling Run().",
            location);
    }

    public static void DC0081(SystemDescription systemDescription, string parallelWriterArgumentName, Location location)
    {
        systemDescription.LogError(
            nameof(DC0081),
            k_ErrorTitle,
            "You are not allowed to pass a Unity.Entities.EntityCommandBuffer.ParallelWriter parameter to the Entities.ForEach() lambda function. " +
            "Instead, please do the following: Pass an Unity.Entities.EntityCommandBuffer parameter, specify an EntityCommandBufferSystem by calling .WithDeferredPlaybackSystem<T>(), " +
            "and finally invoke .ScheduleParallel(). A parallel writer instance will automatically be created and run in said system.",
            location);
    }
    public static void DC0082(SystemDescription systemDescription, Location location, string parameterName)
    {
        systemDescription.LogError(nameof(DC0082), k_ErrorTitle,
            $"{parameterName} is an passed with a `ref` or `in` keyword.  Aspects are already act as reference types and should just be passed in by value.", location);
    }

    public static void DC0083(SystemDescription systemDescription, Location location, LambdaJobKind kind, ScheduleMode scheduleMode)
    {
        systemDescription.LogError(nameof(DC0083), k_ErrorTitle,
            $"Capturing local functions are not allowed in {kind.ToName()}. Consider using {kind.ToNameOfValidAlternativeFeatures(scheduleMode)} instead.", location);
    }

    public static void DC0084(SystemDescription systemDescription, Location location, LambdaJobKind kind, ScheduleMode scheduleMode)
    {
        systemDescription.LogError(nameof(DC0084), k_ErrorTitle,
            $"Anonymous functions are not allowed in {kind.ToName()}. Consider using {kind.ToNameOfValidAlternativeFeatures(scheduleMode)} instead, without burst.", location);
    }

    public static void DC0085(SystemDescription systemDescription, Location location, LambdaJobKind kind, ScheduleMode scheduleMode)
    {
        systemDescription.LogError(nameof(DC0085), k_ErrorTitle,
            $"Defining local functions are not allowed in {kind.ToName()}. Consider using {kind.ToNameOfValidAlternativeFeatures(scheduleMode)} instead.", location);
    }

    public static void DC0086(SystemDescription systemDescription, Location location, IErrorTypeSymbol errorTypeSymbol)
    {
        systemDescription.LogError(nameof(DC0086), k_ErrorTitle,
            $"Failed to load the type of a captured variable. This might be caused by trying to capture a type relying on source-generation. The following candidate reason is given: {errorTypeSymbol.CandidateReason}.", location);
    }
}
