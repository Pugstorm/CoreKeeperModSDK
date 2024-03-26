using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode
{
    /// <summary>
    /// Authoring component which adds the DisableAutomaticPrespawnSectionReporting component to the Entity.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    [HelpURL(Authoring.HelpURLs.DisableAutomaticPrespawnSectionReportingAuthoring)]
    public class DisableAutomaticPrespawnSectionReportingAuthoring : UnityEngine.MonoBehaviour
    {
        [BakingVersion("cmarastoni", 1)]
        class DisableAutomaticPrespawnSectionReportingBaker : Baker<DisableAutomaticPrespawnSectionReportingAuthoring>
        {
            public override void Bake(DisableAutomaticPrespawnSectionReportingAuthoring authoring)
            {
                DisableAutomaticPrespawnSectionReporting component = default(DisableAutomaticPrespawnSectionReporting);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}
