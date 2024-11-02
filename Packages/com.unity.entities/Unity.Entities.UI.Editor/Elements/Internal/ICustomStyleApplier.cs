using Unity.Properties;

namespace Unity.Entities.UI
{
    interface ICustomStyleApplier
    {
        void ApplyStyleAtPath(PropertyPath propertyPath);
    }
}
