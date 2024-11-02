using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common
{
    public static class QueryConstructionErrors
    {
        const string ErrorTitle = "Query Construction Errors";

        public static void SGQC001(SystemDescription systemDescription, Location location, string argumentTypeName, string invokedMethodName)
        {
            systemDescription.LogError(nameof(SGQC001),
                ErrorTitle,
                $"{invokedMethodName}<{argumentTypeName}>() is not supported. {invokedMethodName}<T>() may only be invoked on types that 1) implement IComponentData, ISharedComponentData, IAspect or IBufferElementData," +
                " or 2) are UnityEngine.Object types.",
                location);
        }

        public static void SGQC002(SystemDescription systemDescription, Location location, string queryTypeFullName)
        {
            systemDescription.LogError(
                nameof(SGQC002),
                ErrorTitle,
                $"You invoked both .WithAll<{queryTypeFullName}>() and .WithSharedComponentFilter<{queryTypeFullName}>() in the same `Entities.ForEach` lambda function." +
                $" Please remove the redundant .WithAll<{queryTypeFullName}>() method.",
                location);
        }

        // We were previously giving a very misleading error message when users invoke .WithNone<T>()/.WithAny<T>() together with .WithSharedComponentFilter<T>(), making them think that they invoked .WithAll<T>()
        // even when they did not. We should support specifying the same type in .WithNone<T>()/.WithAny<T>() together with .WithSharedComponentFilter<T>(); but until that happens, we should at least print an
        // accurate error message.
        public static void SGQC003(SystemDescription systemDescription, Location location, string queryTypeFullName)
        {
            systemDescription.LogError(
                nameof(SGQC003),
                ErrorTitle,
                $"Using .WithAny<{queryTypeFullName}>()/.WithNone<{queryTypeFullName}>()/.WithDisabled<{queryTypeFullName}>()/.WithAbsent<{queryTypeFullName}>() and .WithSharedComponentFilter<{queryTypeFullName}>() " +
                "in the same `Entities.ForEach` lambda function is not currently supported.",
                location);
        }

        public static void SGQC004(SystemDescription systemDescription, Location location, string queryGroup1Name, string queryGroup2Name, string componentTypeFullName)
        {
            systemDescription.LogError(
                nameof(SGQC004),
                ErrorTitle,
                $"You specified the same component type ({componentTypeFullName}) in both {queryGroup1Name} and {queryGroup2Name}, which are mutually exclusive.",
                location);
        }

        public static void SGQC005(SystemDescription systemDescription, Location location, string argumentTypeName, string invokedMethodName)
        {
            systemDescription.LogError(nameof(SGQC005),
                ErrorTitle,
                $"{invokedMethodName}<{argumentTypeName}>() is not supported as {invokedMethodName}'s type argument can not itself be a generic type parameter.",
                location);
        }
    }
}
