using System;

namespace Unity.NetCode
{
    /// <summary>
    /// Use this attribute to prevent a GhostComponent from supporting any kind of variants or PrefabType overrides.
    /// Hides this component in the `GhostAuthoringInspectionComponent` window.
    /// Mutually exclusive to <see cref="SupportsPrefabOverridesAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
    public sealed class DontSupportPrefabOverridesAttribute : Attribute
    {
    }
}
