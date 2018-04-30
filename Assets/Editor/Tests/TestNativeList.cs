using UnityEngine;
using System.Collections;
using NUnit.Framework;
using Scripts.Help;

public class TestNativeList
{/*
    [Test]
    public void Add()
    {
        int c;
        var list = new NativeList<int>(10, Unity.Collections.Allocator.Persistent);
        list.Add(10);
        list.Add(1);

        Assert.Catch(() => list[2] = 42);
        Assert.Catch(() => c = list[2]);
        Assert.Catch(() => list[3] = 42);
        Assert.Catch(() => c = list[3]);
        Assert.Catch(() => list[4] = 42);
        Assert.Catch(() => c = list[4]);

        Assert.IsTrue(list[0] == 10);
        Assert.IsTrue(list[1] == 1);

        list.Dispose();
    }

    [Test]
    public void AddWhenSize0()
    {
        int c;
        var list = new NativeList<int>(0, Unity.Collections.Allocator.Persistent);
        list.Add(10);
        list.Add(1);
        list.Add(2);
        list.Add(42);
        list.Add(150);

        Assert.Catch(() => list[5] = 42);
        Assert.Catch(() => c = list[5]);
        Assert.Catch(() => list[6] = 42);
        Assert.Catch(() => c = list[6]);
        Assert.Catch(() => list[7] = 42);
        Assert.Catch(() => c = list[7]);

        Assert.IsTrue(list[0] == 10);
        Assert.IsTrue(list[1] == 1);
        Assert.IsTrue(list[2] == 2);
        Assert.IsTrue(list[3] == 42);
        Assert.IsTrue(list[4] == 150);

        list.Dispose();
    }

    [Test]
    public void AddRange()
    {
        int c;
        var list = new NativeList<int>(0, Unity.Collections.Allocator.Persistent);
        list.AddRange(new int[] { 10, 1, 2, 42, 150 });

        Assert.Catch(() => list[5] = 42);
        Assert.Catch(() => c = list[5]);
        Assert.Catch(() => list[6] = 42);
        Assert.Catch(() => c = list[6]);
        Assert.Catch(() => list[7] = 42);
        Assert.Catch(() => c = list[7]);

        Assert.IsTrue(list[0] == 10);
        Assert.IsTrue(list[1] == 1);
        Assert.IsTrue(list[2] == 2);
        Assert.IsTrue(list[3] == 42);
        Assert.IsTrue(list[4] == 150);

        list.AddRange(new int[] { 105, 15, 25, 425, 1505 });

        Assert.IsTrue(list[5] == 105);
        Assert.IsTrue(list[6] == 15);
        Assert.IsTrue(list[7] == 25);
        Assert.IsTrue(list[8] == 425);
        Assert.IsTrue(list[9] == 1505);

        list.Dispose();
    }

    [Test]
    public void Clear()
    {
        int c;
        var list = new NativeList<int>(10, Unity.Collections.Allocator.Persistent);
        list.Add(10);
        list.Add(1);

        list.Clear();
        Assert.Catch(() => list[0] = 42);
        Assert.Catch(() => list[1] = 42);
        Assert.Catch(() => list[2] = 42);
        Assert.Catch(() => c = list[0]);
        Assert.Catch(() => c = list[1]);
        Assert.Catch(() => c = list[2]);

        Assert.IsTrue(list.Count == 0);

        list.Dispose();
    }*/
}
