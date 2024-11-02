namespace Unity.Entities.Editor
{
    static class EntityManagerEditorExtensions
    {
        public static bool SafeExists(this EntityManager entityManager, Entity entity)
        {
            // If we are in the middle of an exclusive transaction, we can't safely check if an entity exists.
            if (!entityManager.CanBeginExclusiveEntityTransaction())
                entityManager.EndExclusiveEntityTransaction();

#if !ENTITY_STORE_V1
            var maximum = EntityComponentStore.EntityStore.MaximumTheoreticalAmountOfEntities;
#else
            var maximum = entityManager.EntityCapacity;
#endif

            return entity.Index >= 0
                && (uint)entity.Index < (uint)maximum
                && entityManager.Exists(entity);
        }
    }
}
