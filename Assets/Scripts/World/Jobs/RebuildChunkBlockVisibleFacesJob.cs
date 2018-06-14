using Scripts.Help;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World.Jobs
{
    public struct RebuildChunkBlockVisibleFacesJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

        [ReadOnly]
        public NativeArray3D<Voxel> boxThatContainsChunkAndAllNeighboursBorders;

        public void Execute(int index)
        {
            facesVisibleArr.At(index, out int x, out int y, out int z);

            DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
            for (byte i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                Vector3Int vec = dir.ToVecInt();

                if (boxThatContainsChunkAndAllNeighboursBorders[x + vec.x + 1, y + vec.y + 1, z + vec.z + 1].type.IsAir())
                    facesVisible |= dir;
            }
            facesVisibleArr[x, y, z] = facesVisible;
        }
    }
}
