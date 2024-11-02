namespace Unity.Entities.Tests
{
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

    public class ConversionTestCompanionComponent : UnityEngine.MonoBehaviour
    {
        public int SomeValue;
    }

    public class ConversionTestCompanionComponentRequiredByAnotherComponent : UnityEngine.MonoBehaviour
    {
        public int SomeValue;
    }

    [UnityEngine.RequireComponent(typeof(ConversionTestCompanionComponentRequiredByAnotherComponent))]
    public class ConversionTestCompanionComponentWithRequireComponentAttribute : UnityEngine.MonoBehaviour
    {
        public int SomeValue;
    }

#pragma warning restore CS0649
}
