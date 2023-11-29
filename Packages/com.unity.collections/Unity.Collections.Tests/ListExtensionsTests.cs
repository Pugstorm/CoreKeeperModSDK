using System;
using NUnit.Framework;
using Unity.Collections;
using System.Linq;
using Unity.Collections.Tests;

internal class ListExtensionsTests : CollectionsTestCommonBase
{
// https://unity3d.atlassian.net/browse/DOTSR-1432
    [Test]
    public void ListExtensions_RemoveSwapBack_Item()
    {
        var list = new[] { 'a', 'b', 'c', 'd' }.ToList();

        Assert.True(list.RemoveSwapBack('b'));
        CollectionAssert.AreEqual(new[] { 'a', 'd', 'c', }, list);

        Assert.True(list.RemoveSwapBack('c'));
        CollectionAssert.AreEqual(new[] { 'a', 'd' }, list);

        Assert.False(list.RemoveSwapBack('z'));
        CollectionAssert.AreEqual(new[] { 'a', 'd' }, list);

        Assert.True(list.RemoveSwapBack('a'));
        CollectionAssert.AreEqual(new[] { 'd' }, list);

        Assert.True(list.RemoveSwapBack('d'));
        CollectionAssert.IsEmpty(list);

        Assert.False(list.RemoveSwapBack('d'));
        CollectionAssert.IsEmpty(list);
    }

    [Test]
    public void ListExtensions_RemoveSwapBack_Predicate()
    {
        var list = new[] { 'a', 'b', 'c', 'd' }.ToList();

        Assert.True(list.RemoveSwapBack(c => c == 'b'));
        CollectionAssert.AreEqual(new[] { 'a', 'd', 'c', }, list);

        Assert.True(list.RemoveSwapBack(c => c == 'c'));
        CollectionAssert.AreEqual(new[] { 'a', 'd' }, list);

        Assert.False(list.RemoveSwapBack(c => c == 'z'));
        CollectionAssert.AreEqual(new[] { 'a', 'd' }, list);

        Assert.True(list.RemoveSwapBack(c => c == 'a'));
        CollectionAssert.AreEqual(new[] { 'd' }, list);

        Assert.True(list.RemoveSwapBack(c => c == 'd'));
        CollectionAssert.IsEmpty(list);

        Assert.False(list.RemoveSwapBack(c => c == 'd'));
        CollectionAssert.IsEmpty(list);
    }

// https://unity3d.atlassian.net/browse/DOTSR-1432
    [Test]
    public void ListExtensions_RemoveAtSwapBack()
    {
        var list = new[] { 'a', 'b', 'c', 'd' }.ToList();

        list.RemoveAtSwapBack(1);
        CollectionAssert.AreEqual(new[] { 'a', 'd', 'c', }, list);

        list.RemoveAtSwapBack(2);
        CollectionAssert.AreEqual(new[] { 'a', 'd' }, list);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAtSwapBack(12));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAtSwapBack(-5));

        list.RemoveAtSwapBack(0);
        CollectionAssert.AreEqual(new[] { 'd' }, list);

        list.RemoveAtSwapBack(0);
        CollectionAssert.IsEmpty(list);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAtSwapBack(0));
    }

    [Test]
    public void ListExtensions_ToNativeList()
    {
        var list = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }.ToList();
        var native = list.ToNativeList(Allocator.Persistent);

        for (int i = 0; i < native.Length; ++i)
        {
            Assert.AreEqual(i, native[i]);
        }

        native.Dispose();
    }

    [Test]
    public void ListExtensions_ToNativeArray()
    {
        var list = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }.ToList();
        var native = list.ToNativeArray(Allocator.Persistent);

        for (int i = 0; i < native.Length; ++i)
        {
            Assert.AreEqual(i, native[i]);
        }

        native.Dispose();
    }
}
