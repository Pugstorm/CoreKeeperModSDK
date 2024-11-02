using System;
using Unity.Entities.Conversion;

namespace Unity.Entities.Editor
{
    struct EntityBakingData : IDisposable, IEquatable<EntityBakingData>
    {
        public static readonly EntityBakingData Null = default;

        public Entity PrimaryEntity;
        public Entity[] AdditionalEntities;
        public EntityManager EntityManager;

        public void Dispose()
        {
        }

        public static bool operator ==(EntityBakingData lhs, EntityBakingData rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(EntityBakingData lhs, EntityBakingData rhs)
        {
            return !(lhs == rhs);
        }

        public bool Equals(EntityBakingData other)
        {
            return PrimaryEntity.Equals(other.PrimaryEntity) && Equals(AdditionalEntities, other.AdditionalEntities) && Equals(EntityManager, other.EntityManager);
        }

        public override bool Equals(object obj)
        {
            return obj is EntityBakingData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = PrimaryEntity.GetHashCode();
                hashCode = (hashCode * 397) ^ (AdditionalEntities != null ? AdditionalEntities.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (EntityManager != default ? EntityManager.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
