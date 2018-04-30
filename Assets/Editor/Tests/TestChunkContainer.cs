using NUnit.Framework;
using Scripts.World;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestChunkContainer
{
    private ChunkContainer chunks;
    private RegularChunk[,] ch;

    [Test]
    public void Initialization()
    {
        ch = new RegularChunk[5, 5];
        chunks = new ChunkContainer(5, 5);
        chunks.InitializeStartingLevel(0, ch);
        Assert.IsTrue(chunks.MaxHeight == 0);
        Assert.IsTrue(chunks.MinHeight == 0);
    }

    [Test]
    public void AddUp()
    {
        Initialization();

        chunks.AddLevel(true, ch);
        Assert.IsTrue(chunks.MaxHeight == 1, $"{chunks.MaxHeight} does not equal {1}");
        Assert.IsTrue(chunks.MinHeight == 0, $"{chunks.MinHeight} does not equal {0}");
    }

    [Test]
    public void AddDown()
    {
        Initialization();

        chunks.AddLevel(false, ch);
        Assert.IsTrue(chunks.MaxHeight == 0, $"{chunks.MaxHeight} does not equal {0}");
        Assert.IsTrue(chunks.MinHeight == -1, $"{chunks.MinHeight} does not equal {-1}");
    }

    [Test]
    public void AddUp5()
    {
        Initialization();
        for (int i = 0; i < 5; i++)
        {
            chunks.AddLevel(true, ch);
        }
        Assert.IsTrue(chunks.MaxHeight == 5, $"{chunks.MaxHeight} does not equal {5}");
        Assert.IsTrue(chunks.MinHeight == 0, $"{chunks.MinHeight} does not equal {0}");
    }

    [Test]
    public void AddDown5()
    {
        Initialization();
        for (int i = 0; i < 5; i++)
        {
            chunks.AddLevel(false, ch);
        }
        Assert.IsTrue(chunks.MaxHeight == 0, $"{chunks.MaxHeight} does not equal {0}");
        Assert.IsTrue(chunks.MinHeight == -5, $"{chunks.MinHeight} does not equal {-5}");
    }

    [Test]
    public void ContainsHeight()
    {
        Initialization();
        for (int i = 0; i < 5; i++)
        {
            chunks.AddLevel(true, ch);
        }
        for (int i = 0; i < 5; i++)
        {
            chunks.AddLevel(false, ch);
        }
        Assert.IsTrue(chunks.ContainsHeight(0) == true);
        Assert.IsTrue(chunks.ContainsHeight(1) == true);
        Assert.IsTrue(chunks.ContainsHeight(2) == true);
        Assert.IsTrue(chunks.ContainsHeight(3) == true);
        Assert.IsTrue(chunks.ContainsHeight(4) == true);
        Assert.IsTrue(chunks.ContainsHeight(5) == true);
        Assert.IsTrue(chunks.ContainsHeight(6) == false);
        Assert.IsTrue(chunks.ContainsHeight(7) == false);
        Assert.IsTrue(chunks.ContainsHeight(-1) == true);
        Assert.IsTrue(chunks.ContainsHeight(-2) == true);
        Assert.IsTrue(chunks.ContainsHeight(-3) == true);
        Assert.IsTrue(chunks.ContainsHeight(-4) == true);
        Assert.IsTrue(chunks.ContainsHeight(-5) == true);
        Assert.IsTrue(chunks.ContainsHeight(-6) == false);
    }

    [Test]
    public void Remove()
    {
        Initialization();
        for (int i = 0; i < 5; i++)
        {
            chunks.AddLevel(true, ch);
        }
        for (int i = 0; i < 5; i++)
        {
            chunks.AddLevel(false, ch);
        }
        chunks.RemoveLevel(true);
        Assert.IsTrue(chunks.ContainsHeight(5) == false);
        chunks.RemoveLevel(true);
        Assert.IsTrue(chunks.ContainsHeight(4) == false);
        chunks.RemoveLevel(false);
        Assert.IsTrue(chunks.ContainsHeight(-5) == false);
        chunks.RemoveLevel(false);
        Assert.IsTrue(chunks.ContainsHeight(-4) == false);

        Assert.IsTrue(chunks.ContainsHeight(-3) == true);
        Assert.IsTrue(chunks.ContainsHeight(3) == true);

        Assert.IsTrue(chunks.MaxHeight == 3);
        Assert.IsTrue(chunks.MinHeight == -3);
    }
}
