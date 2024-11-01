using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestGameObjectPropertiesChangeAuthoring : MonoBehaviour
    {
        public GameObject reference;

        class Baker : Baker<TestGameObjectPropertiesChangeAuthoring>
        {
            public override void Bake(TestGameObjectPropertiesChangeAuthoring authoring)
            {
				DependsOn(authoring.reference);
            }
        }
    }
}
