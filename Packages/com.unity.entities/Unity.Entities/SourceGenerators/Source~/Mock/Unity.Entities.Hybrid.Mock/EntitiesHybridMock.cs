using UnityEngine;

namespace Unity.Entities.Hybrid
{
    public class EntitiesHybridMock { }
}

namespace Unity.Entities
{
    public interface IConvertGameObjectToEntity
    {
        void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem);
    }

    public abstract partial class GameObjectConversionSystem : SystemBase { }

    public abstract unsafe class IBaker
    {
        internal struct BakerExecutionState { }
    }

    public abstract unsafe class Baker<TAuthoringType> : IBaker
        where TAuthoringType : Component
    {
        public abstract void Bake(TAuthoringType authoring);

        internal void InvokeBake(in BakerExecutionState state) => throw default;

        internal Type GetAuthoringType() => throw default;
    }
}
