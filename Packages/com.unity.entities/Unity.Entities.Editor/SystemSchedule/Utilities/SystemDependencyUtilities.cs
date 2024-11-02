using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities.Editor
{
    static class SystemDependencyUtilities
    {
        public enum ECBSystemSchedule
        {
            None,
            Begin,
            End
        }

        /// <summary>
        /// Check given ECB system is at the beginning or the end.
        /// </summary>
        /// <param name="systemType">Given system type</param>
        /// <returns>Order of the given ECB system defined by <see cref="ECBSystemSchedule"/>></returns>
        public static ECBSystemSchedule GetECBSystemScheduleInfo(Type systemType)
        {
            if (!typeof(EntityCommandBufferSystem).IsAssignableFrom(systemType))
                return ECBSystemSchedule.None;

            var attrArray = TypeManager.GetSystemAttributes(systemType, typeof(UpdateInGroupAttribute));
            if (attrArray == null || attrArray.Length == 0)
                return ECBSystemSchedule.None;

            var updateInGroupAttribute = (UpdateInGroupAttribute)attrArray[0];
            if (updateInGroupAttribute.OrderFirst)
                return ECBSystemSchedule.Begin;
            if (updateInGroupAttribute.OrderLast)
                return ECBSystemSchedule.End;

            return ECBSystemSchedule.None;
        }

        /// <summary>
        /// Get <see cref="Type"/> for update before/after system list for given system type.
        /// <param name="systemType">The given <see cref="ComponentSystemBase"/>.</param>
        /// </summary>
        public static IEnumerable<Type> GetSystemAttributes<TAttribute>(Type systemType)
            where TAttribute : System.Attribute
        {
            var attrArray = TypeManager.GetSystemAttributes(systemType, typeof(TAttribute)).OfType<TAttribute>();
            foreach (var attr in attrArray)
            {
                switch (attr)
                {
                    case UpdateAfterAttribute afterDep:
                        yield return afterDep.SystemType;
                        break;
                    case UpdateBeforeAttribute beforeDep:
                        yield return beforeDep.SystemType;
                        break;
                }
            }
        }
    }
}
