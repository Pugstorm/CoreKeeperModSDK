using Unity.Entities;
using UnityEngine;

namespace Unity.Scenes.Editor
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SceneSystemGroup))]
    partial class LiveConversionEditorSystemGroup : ComponentSystemGroup
    {
        protected override void OnUpdate()
        {
            if (!LiveConversionEditorSettings.LiveConversionEnabled)
                return;
            base.OnUpdate();
        }
    }
}
