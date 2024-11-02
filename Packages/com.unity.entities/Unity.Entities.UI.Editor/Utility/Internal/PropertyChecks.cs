using System;
using Unity.Properties;

namespace Unity.Entities.UI
{
    static class PropertyChecks
    {
        public static string GetNotConstructableWarningMessage(Type type)
            => $"Could not create an instance of type `{TypeUtility.GetTypeDisplayName(type)}`. A public parameter-less constructor or an explicit construction method is required.";

        public static string GetNotAssignableWarningMessage(Type type, Type assignableTo)
            => $"Could not create an instance of type `{TypeUtility.GetTypeDisplayName(type)}`: Type must be assignable to `{TypeUtility.GetTypeDisplayName(assignableTo)}`";
    }
}
