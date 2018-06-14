using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Scripts.Help;

public class TestDirectionsHelper
{
    [Test]
    public void Test_DirectionToVec()
    {
        Assert.IsTrue(DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Right) == new Vector3Int(1, 0, 0),
            $"{DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Right).ToString()} is not {new Vector3Int(1, 0, 0).ToString()}");
        Assert.IsTrue(DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Left) == new Vector3Int(-1, 0, 0));
        Assert.IsTrue(DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Up) == new Vector3Int(0, 1, 0));
        Assert.IsTrue(DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Down) == new Vector3Int(0, -1, 0));
        Assert.IsTrue(DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Back) == new Vector3Int(0, 0, -1));
        Assert.IsTrue(DirectionsHelper.ToVecInt(DirectionsHelper.BlockDirectionFlag.Front) == new Vector3Int(0, 0, 1));
    }
}
