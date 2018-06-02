using Scripts.Help;
using Scripts.World;
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
        public Vector3Int chunkPos;

        [ReadOnly]
        public NativeArray3D<Voxel> voxels,
            voxelsUp, voxelsDown, voxelsLeft, voxelsRight, voxelsBack, voxelsFront;

        public void Execute(int currentIndex)
        {
            int x, y, z;
            facesVisibleArr.At(currentIndex, out x, out y, out z);

            DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
            for (byte i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                Vector3Int vec = dir.DirectionToVec();

                if (x + vec.x < VoxelWorld._chunkSize && y + vec.y < VoxelWorld._chunkSize && z + vec.z < VoxelWorld._chunkSize
                    &&
                    x + vec.x >= 0 && y + vec.y >= 0 && z + vec.z >= 0)
                {
                    if (voxels[x + vec.x, y + vec.y, z + vec.z].type.IsTransparent())
                        facesVisible |= dir;
                }
                else
                {
                    var blockInd = (new Vector3Int(x, y, z) + vec);

                    if (blockInd.x >= VoxelWorld._chunkSize) blockInd.x = 0;
                    else if (blockInd.x < 0) blockInd.x = VoxelWorld._chunkSize - 1;

                    if (blockInd.y >= VoxelWorld._chunkSize) blockInd.y = 0;
                    else if (blockInd.y < 0) blockInd.y = VoxelWorld._chunkSize - 1;

                    if (blockInd.z >= VoxelWorld._chunkSize) blockInd.z = 0;
                    else if (blockInd.z < 0) blockInd.z = VoxelWorld._chunkSize - 1;

                    NativeArray3D<Voxel> ch;
                    switch (dir)
                    {
                        case DirectionsHelper.BlockDirectionFlag.None: ch = new NativeArray3D<Voxel>(); break;
                        case DirectionsHelper.BlockDirectionFlag.Up: ch = voxelsUp; break;
                        case DirectionsHelper.BlockDirectionFlag.Down: ch = voxelsDown; break;
                        case DirectionsHelper.BlockDirectionFlag.Left: ch = voxelsLeft; break;
                        case DirectionsHelper.BlockDirectionFlag.Right: ch = voxelsRight; break;
                        case DirectionsHelper.BlockDirectionFlag.Back: ch = voxelsBack; break;
                        case DirectionsHelper.BlockDirectionFlag.Front: ch = voxelsFront; break;
                        default: ch = new NativeArray3D<Voxel>(); break;
                    }

                    if ((ch[blockInd.x, blockInd.y, blockInd.z]).type.IsTransparent())
                        facesVisible |= dir;
                }
            }
            facesVisibleArr[x, y, z] = facesVisible;
        }
    }
}
