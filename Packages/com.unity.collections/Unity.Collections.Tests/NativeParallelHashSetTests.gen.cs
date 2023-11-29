using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;

internal class NativeParallelHashSetTestsGenerated : CollectionsTestFixture
{
    static void ExpectedCount<T>(ref NativeParallelHashSet<T> container, int expected)
        where T : unmanaged, IEquatable<T>
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count());
    }


    [Test]
    public void NativeParallelHashSet_NativeParallelHashSet_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeParallelHashSet_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeParallelHashSet_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeParallelHashSet_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeParallelHashSet_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeParallelHashSet_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void NativeParallelHashSet_UnsafeParallelHashSet_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeParallelHashSet_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeParallelHashSet_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeParallelHashSet_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeParallelHashSet_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeParallelHashSet_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void NativeParallelHashSet_NativeList_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeList_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeList_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeList_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeList_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_NativeList_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void NativeParallelHashSet_UnsafeList_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeList_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeList_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeList_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeList_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_UnsafeList_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void NativeParallelHashSet_FixedList32Bytes_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList32Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList32Bytes_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList32Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList32Bytes_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList32Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList32Bytes_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList32Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList32Bytes_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList32Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList32Bytes_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList32Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void NativeParallelHashSet_FixedList64Bytes_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList64Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList64Bytes_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList64Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList64Bytes_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList64Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList64Bytes_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList64Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList64Bytes_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList64Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList64Bytes_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList64Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void NativeParallelHashSet_FixedList128Bytes_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList128Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList128Bytes_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList128Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList128Bytes_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList128Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList128Bytes_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList128Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList128Bytes_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList128Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList128Bytes_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList128Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void NativeParallelHashSet_FixedList512Bytes_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList512Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList512Bytes_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList512Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList512Bytes_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList512Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList512Bytes_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList512Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList512Bytes_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList512Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList512Bytes_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList512Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void NativeParallelHashSet_FixedList4096Bytes_EIU_ExceptWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList4096Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList4096Bytes_EIU_ExceptWith_AxB()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList4096Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList4096Bytes_EIU_IntersectWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList4096Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList4096Bytes_EIU_IntersectWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList4096Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList4096Bytes_EIU_UnionWith_Empty()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList4096Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashSet_FixedList4096Bytes_EIU_UnionWith()
    {
        var container = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList4096Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    static void ExpectedCount<T>(ref UnsafeParallelHashSet<T> container, int expected)
        where T : unmanaged, IEquatable<T>
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count());
    }


    [Test]
    public void UnsafeParallelHashSet_NativeParallelHashSet_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeParallelHashSet_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeParallelHashSet_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeParallelHashSet_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeParallelHashSet_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeParallelHashSet_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void UnsafeParallelHashSet_UnsafeParallelHashSet_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeParallelHashSet_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeParallelHashSet_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeParallelHashSet_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeParallelHashSet_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeParallelHashSet_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void UnsafeParallelHashSet_NativeList_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeList_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeList_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeList_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeList_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_NativeList_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new NativeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void UnsafeParallelHashSet_UnsafeList_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeList_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeList_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeList_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeList_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
        other.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_UnsafeList_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new UnsafeList<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
        other.Dispose();
    }


    [Test]
    public void UnsafeParallelHashSet_FixedList32Bytes_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList32Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList32Bytes_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList32Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList32Bytes_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList32Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList32Bytes_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList32Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList32Bytes_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList32Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList32Bytes_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList32Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void UnsafeParallelHashSet_FixedList64Bytes_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList64Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList64Bytes_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList64Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList64Bytes_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList64Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList64Bytes_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList64Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList64Bytes_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList64Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList64Bytes_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList64Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void UnsafeParallelHashSet_FixedList128Bytes_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList128Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList128Bytes_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList128Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList128Bytes_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList128Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList128Bytes_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList128Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList128Bytes_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList128Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList128Bytes_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList128Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void UnsafeParallelHashSet_FixedList512Bytes_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList512Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList512Bytes_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList512Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList512Bytes_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList512Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList512Bytes_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList512Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList512Bytes_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList512Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList512Bytes_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList512Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
    [Test]
    public void UnsafeParallelHashSet_FixedList4096Bytes_EIU_ExceptWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList4096Bytes<int>() { };
        container.ExceptWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList4096Bytes_EIU_ExceptWith_AxB()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList4096Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.ExceptWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList4096Bytes_EIU_IntersectWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList4096Bytes<int>() { };
        container.IntersectWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList4096Bytes_EIU_IntersectWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList4096Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.IntersectWith(other);

        ExpectedCount(ref container, 3);
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList4096Bytes_EIU_UnionWith_Empty()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var other = new FixedList4096Bytes<int>() { };
        container.UnionWith(other);

        ExpectedCount(ref container, 0);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_FixedList4096Bytes_EIU_UnionWith()
    {
        var container = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var other = new FixedList4096Bytes<int>() { 3, 4, 5, 6, 7, 8 };
        container.UnionWith(other);

        ExpectedCount(ref container, 9);
        Assert.True(container.Contains(0));
        Assert.True(container.Contains(1));
        Assert.True(container.Contains(2));
        Assert.True(container.Contains(3));
        Assert.True(container.Contains(4));
        Assert.True(container.Contains(5));
        Assert.True(container.Contains(6));
        Assert.True(container.Contains(7));
        Assert.True(container.Contains(8));

        container.Dispose();
    }
}
