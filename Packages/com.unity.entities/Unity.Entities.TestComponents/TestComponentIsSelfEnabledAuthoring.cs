using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestComponentIsSelfEnabledAuthoring : MonoBehaviour
    {
        public struct SelfEnabled : IComponentData
        {

        }

		public void Update()
		{

		}

        class Baker : Baker<TestComponentIsSelfEnabledAuthoring>
        {
            public override void Bake(TestComponentIsSelfEnabledAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
				AddComponent<SelfEnabled>(entity);
            }
        }
    }
}
