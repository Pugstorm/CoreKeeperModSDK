using UnityEngine;
using Unity.Entities;

public class TestNetCodeAuthoring : MonoBehaviour
{
    public interface IConverter
    {
        void Bake(GameObject gameObject, IBaker baker);
    }
    public IConverter Converter;
}

class TestNetCodeAuthoringBaker : Baker<TestNetCodeAuthoring>
{
    public override void Bake(TestNetCodeAuthoring authoring)
    {
        if (authoring.Converter != null)
            authoring.Converter.Bake(authoring.gameObject, this);
    }
}
