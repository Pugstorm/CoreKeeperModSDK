using System;

namespace Unity.Entities.Build
{
    // Ensure a baking system is running during baking even it is in an assembly that is excluded from baking via from BakingSystemFilterSettings excluded assemblies list.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal class AlwaysIncludeBakingSystemAttribute : Attribute { }
}
