using NUnit.Framework;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class MathTests
{
    [Test]
    public void MathTest_ContainsPoint()
    {
        var aabb = new AABB { Center = new float3(0.0f), Extents = new float3(1.0f) };

        Assert.IsTrue(aabb.Contains(new float3(0.0f)));
        Assert.IsTrue(aabb.Contains(aabb.Min));
        Assert.IsTrue(aabb.Contains(aabb.Max));
        Assert.IsTrue(aabb.Contains(new float3(0.5f)));
        Assert.IsTrue(aabb.Contains(new float3(-0.5f)));
        Assert.IsTrue(aabb.Contains(new float3(-1.0f, 1.0f, 1.0f)));
        Assert.IsTrue(aabb.Contains(new float3(1.0f, -1.0f, 1.0f)));
        Assert.IsTrue(aabb.Contains(new float3(1.0f, 1.0f, -1.0f)));
        Assert.IsFalse(aabb.Contains(new float3(1.0f + EPSILON, 1.0f, 1.0f)));
        Assert.IsFalse(aabb.Contains(new float3(1.0f, 1.0f + EPSILON, 1.0f)));
        Assert.IsFalse(aabb.Contains(new float3(1.0f, 1.0f, 1.0f + EPSILON)));
        Assert.IsFalse(aabb.Contains(new float3(-1.0f - EPSILON, -1.0f, -1.0f)));
        Assert.IsFalse(aabb.Contains(new float3(-1.0f, -1.0f - EPSILON, -1.0f)));
        Assert.IsFalse(aabb.Contains(new float3(-1.0f, -1.0f, -1.0f - EPSILON)));
    }

    [Test]
    public static void MathTest_ContainsAabb_Trivial()
    {
        var aabb1 = new AABB();
        var aabb2 = new AABB();

        Assert.IsTrue(aabb1.Contains(aabb2));
        Assert.IsTrue(aabb2.Contains(aabb1));
    }

    [Test]
    public static void MathTest_ContainsAabb()
    {
        var aabb1 = new AABB { Center = new float3(0.0f), Extents = new float3(1.0f) };
        var aabb2 = new AABB { Center = new float3(0.0f), Extents = new float3(0.5f) };

        Assert.IsTrue(aabb1.Contains(aabb2));
        Assert.IsFalse(aabb2.Contains(aabb1));
    }

    static AABB FromMinMax(float3 min, float3 max) => new AABB { Center = (min + max) * 0.5f, Extents = (max - min) * 0.5f };

    [Test]
    public static void MathTest_ContainsAabb_BarelyNotContained()
    {
        var minDiff = EPSILON * 2.0f;
        var aabb1 = FromMinMax(new float3(-1.0f), new float3(1.0f));
        var aabb2 = FromMinMax(new float3(-1.0f), new float3(1.0f + minDiff, 1.0f, 1.0f));
        var aabb3 = FromMinMax(new float3(-1.0f), new float3(1.0f, 1.0f + minDiff, 1.0f));
        var aabb4 = FromMinMax(new float3(-1.0f), new float3(1.0f, 1.0f, 1.0f + minDiff));
        var aabb5 = FromMinMax(new float3(-1.0f - minDiff, -1.0f, -1.0f), new float3(1.0f));
        var aabb6 = FromMinMax(new float3(-1.0f, -1.0f - minDiff, -1.0f), new float3(1.0f));
        var aabb7 = FromMinMax(new float3(-1.0f, -1.0f, -1.0f - minDiff), new float3(1.0f));

        Assert.IsFalse(aabb1.Contains(aabb2));
        Assert.IsFalse(aabb1.Contains(aabb3));
        Assert.IsFalse(aabb1.Contains(aabb4));
        Assert.IsFalse(aabb1.Contains(aabb5));
        Assert.IsFalse(aabb1.Contains(aabb6));
        Assert.IsFalse(aabb1.Contains(aabb7));
    }

}
