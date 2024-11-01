using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public class MissingConversionMonobehaviourTest : MonoBehaviour
    {
        [RegisterBinding(typeof(MissingConversionComponentTest), "Field1")]
        public int Field1;
    }

    public struct MissingConversionComponentTest : IComponentData
    {
        public int Field1;
    }
}
