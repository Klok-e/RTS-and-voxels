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
        public DirectionsHelper.BlockDirectionFlag availableChunks;

        [ReadOnly]
        public Vector3Int chunkPos;

        [ReadOnly]
        public int chunkSize;

        [ReadOnly]
        public NativeArray3D<Voxel> voxels,
            voxelsUp, voxelsDown, voxelsLeft, voxelsRight, voxelsBack, voxelsFront;

        public void Execute(int currentIndex)
        {
            facesVisibleArr.At(currentIndex, out int x, out int y, out int z);

            DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
            for (byte i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                Vector3Int vec = dir.DirectionToVec();

                if (x + vec.x < chunkSize && y + vec.y < chunkSize && z + vec.z < chunkSize
                    &&
                    x + vec.x >= 0 && y + vec.y >= 0 && z + vec.z >= 0)
                {
                    if (voxels[x + vec.x, y + vec.y, z + vec.z].type.IsAir())
                        facesVisible |= dir;
                }
                else if ((dir & availableChunks) == 0)
                {
                    facesVisible |= dir;
                }
                else
                {
                    var blockInd = (new Vector3Int(x, y, z) + vec);

                    if (blockInd.x >= chunkSize) blockInd.x = 0;
                    else if (blockInd.x < 0) blockInd.x = chunkSize - 1;

                    if (blockInd.y >= chunkSize) blockInd.y = 0;
                    else if (blockInd.y < 0) blockInd.y = chunkSize - 1;

                    if (blockInd.z >= chunkSize) blockInd.z = 0;
                    else if (blockInd.z < 0) blockInd.z = chunkSize - 1;

                    NativeArray3D<Voxel> ch;
                    switch (dir)
                    {
                        case DirectionsHelper.BlockDirectionFlag.Up: ch = voxelsUp; break;
                        case DirectionsHelper.BlockDirectionFlag.Down: ch = voxelsDown; break;
                        case DirectionsHelper.BlockDirectionFlag.Left: ch = voxelsLeft; break;
                        case DirectionsHelper.BlockDirectionFlag.Right: ch = voxelsRight; break;
                        case DirectionsHelper.BlockDirectionFlag.Back: ch = voxelsBack; break;
                        case DirectionsHelper.BlockDirectionFlag.Front: ch = voxelsFront; break;
                        default: throw new Exception();
                    }

                    if ((ch[blockInd.x, blockInd.y, blockInd.z]).type.IsAir())
                        facesVisible |= dir;
                }
            }
            facesVisibleArr[x, y, z] = facesVisible;
        }
    }
}
