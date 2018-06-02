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
    public struct RebuildVisibilityOfVoxelsJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray3D<BlittableBool> visibilityArrOfVoxelsToRebuild;

        [ReadOnly]
        public Vector3Int chunkPos;

        [ReadOnly]
        public int mapMaxX, mapMaxZ;

        [ReadOnly]
        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

        private static bool DoesVoxelExceedBordersOfMapInDirection(Vector3Int chunkPos, Vector3Int voxelInd, DirectionsHelper.BlockDirectionFlag dirToLook, int mapMaxX, int mapMaxZ)
        {
            int x = voxelInd.x,
                y = voxelInd.y,
                z = voxelInd.z;

            var vec = dirToLook.DirectionToVec();
            if (x + vec.x < VoxelWorld._chunkSize && y + vec.y < VoxelWorld._chunkSize && z + vec.z < VoxelWorld._chunkSize
                &&
                x + vec.x >= 0 && y + vec.y >= 0 && z + vec.z >= 0)
            {
                return false;
            }
            else
            {
                var adjacentChunkPos = chunkPos + vec;
                return (adjacentChunkPos.x >= 0 && adjacentChunkPos.z >= 0
                    &&
                    adjacentChunkPos.x < mapMaxX && adjacentChunkPos.z < mapMaxZ) ? false : true;
            }
        }

        public void Execute(int index)
        {
            var facesVisible = facesVisibleArr[index];
            visibilityArrOfVoxelsToRebuild.At(index, out int x, out int y, out int z);
            Vector3Int voxelIndex = new Vector3Int(x, y, z);

            BlittableBool isVisible = BlittableBool.False;
            if ((facesVisible & DirectionsHelper.BlockDirectionFlag.Up) != 0
                ||
                (facesVisible & DirectionsHelper.BlockDirectionFlag.Down) != 0)
            {
                isVisible = BlittableBool.True;
            }
            else if ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Right)) != 0
                ||
                (facesVisible & (DirectionsHelper.BlockDirectionFlag.Left)) != 0
                ||
                (facesVisible & (DirectionsHelper.BlockDirectionFlag.Front)) != 0
                ||
                (facesVisible & (DirectionsHelper.BlockDirectionFlag.Back)) != 0)//if any of these flags is set
            {
                //if any of the voxel's visible faces faces voxel that is not out of borders then block must be visible
                if (((facesVisible & (DirectionsHelper.BlockDirectionFlag.Right)) != 0
                    &&
                    !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Right, mapMaxX, mapMaxZ))
                    ||
                    ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Left)) != 0
                    &&
                    !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Left, mapMaxX, mapMaxZ))
                    ||
                    ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Front)) != 0
                    &&
                    !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Front, mapMaxX, mapMaxZ))
                    ||
                    ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Back)) != 0
                    &&
                    !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Back, mapMaxX, mapMaxZ)))
                {
                    isVisible = BlittableBool.True;
                }
            }
            visibilityArrOfVoxelsToRebuild[index] = isVisible;
        }
    }
}
