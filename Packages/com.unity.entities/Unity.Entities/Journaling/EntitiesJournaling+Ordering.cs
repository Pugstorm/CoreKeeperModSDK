#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        public enum Ordering
        {
            Ascending,
            Descending
        }
    }
}
#endif
