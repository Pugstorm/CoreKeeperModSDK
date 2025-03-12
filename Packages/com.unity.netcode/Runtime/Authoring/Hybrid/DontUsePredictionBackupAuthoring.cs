using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode
{
    public class DontUsePredictionBackupAuthoringBaker : Baker<GhostAuthoringComponent>
    {
        public override void Bake(GhostAuthoringComponent authoring)
        {
            if (authoring.DontUsePredictionBackup)
            {
                AddComponent<DontUsePredictionBackup>(GetEntity(TransformUsageFlags.None));
            }
        }
    }
}