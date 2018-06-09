using Scripts.Help;
using Scripts.World;
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World.Jobs
{
    public struct ConstructMeshJob : IJob
    {
        [ReadOnly]
        public NativeArray3D<Voxel> voxels;

        [ReadOnly]
        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;

        [ReadOnly]
        public DirectionsHelper.BlockDirectionFlag availableChunks;

        [ReadOnly]
        public NativeArray3D<VoxelLightingLevel> lightingLevels,
            lightingLevelsUp, lightingLevelsDown, lightingLevelsLeft, lightingLevelsRight, lightingLevelsBack, lightingLevelsFront;

        [WriteOnly]
        public NativeMeshData meshData;

        public void Execute()
        {
            for (int i = 0; i < VoxelWorldController._chunkSize * VoxelWorldController._chunkSize * VoxelWorldController._chunkSize; i++)
            {
                voxels.At(i, out int x, out int y, out int z);
                if (voxels[x, y, z].type != VoxelType.Air)
                {
                    var col = voxels[x, y, z].ToColor();

                    CreateCube(ref meshData, new Vector3(x, y, z) * VoxelWorldController._blockSize, voxelsVisibleFaces[x, y, z], col, new Vector3Int(x, y, z));
                }
            }
        }

        #region Mesh generation

        private void CreateCube(ref NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Color color, Vector3Int blockPos)
        {
            for (int i = 0; i < 6; i++)
            {
                var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                if ((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                    CreateFace(ref mesh, pos, curr, color, blockPos);
            }
        }

        private void CreateFace(ref NativeMeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Color color, Vector3Int blockPos)
        {
            var vec = dir.DirectionToVec();
            var nextBlockPos = blockPos + vec;

            byte light;
            if (nextBlockPos.x < VoxelWorldController._chunkSize && nextBlockPos.y < VoxelWorldController._chunkSize && nextBlockPos.z < VoxelWorldController._chunkSize
                    &&
                    nextBlockPos.x >= 0 && nextBlockPos.y >= 0 && nextBlockPos.z >= 0)
            {
                light = lightingLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level;
            }
            else if ((dir & availableChunks) == 0)
            {
                light = 0;
            }
            else
            {
                if (nextBlockPos.x >= VoxelWorldController._chunkSize) nextBlockPos.x = 0;
                else if (nextBlockPos.x < 0) nextBlockPos.x = VoxelWorldController._chunkSize - 1;

                if (nextBlockPos.y >= VoxelWorldController._chunkSize) nextBlockPos.y = 0;
                else if (nextBlockPos.y < 0) nextBlockPos.y = VoxelWorldController._chunkSize - 1;

                if (nextBlockPos.z >= VoxelWorldController._chunkSize) nextBlockPos.z = 0;
                else if (nextBlockPos.z < 0) nextBlockPos.z = VoxelWorldController._chunkSize - 1;

                NativeArray3D<VoxelLightingLevel> ch;
                switch (dir)
                {
                    case DirectionsHelper.BlockDirectionFlag.Up: ch = lightingLevelsUp; break;
                    case DirectionsHelper.BlockDirectionFlag.Down: ch = lightingLevelsDown; break;
                    case DirectionsHelper.BlockDirectionFlag.Left: ch = lightingLevelsLeft; break;
                    case DirectionsHelper.BlockDirectionFlag.Right: ch = lightingLevelsRight; break;
                    case DirectionsHelper.BlockDirectionFlag.Back: ch = lightingLevelsBack; break;
                    case DirectionsHelper.BlockDirectionFlag.Front: ch = lightingLevelsFront; break;
                    default: throw new Exception();
                }
                light = ch[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level;
            }

            color *= (float)light / 32;

            var startIndex = mesh._vertices.Length;

            Quaternion rotation = Quaternion.LookRotation(vec);

            mesh._colors.Add(color);
            mesh._colors.Add(color);
            mesh._colors.Add(color);
            mesh._colors.Add(color);

            mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);

            Vector3Int normal = dir.DirectionToVec();

            mesh._normals.Add(normal);
            mesh._normals.Add(normal);
            mesh._normals.Add(normal);
            mesh._normals.Add(normal);

            mesh._triangles.Add(startIndex + 0);
            mesh._triangles.Add(startIndex + 1);
            mesh._triangles.Add(startIndex + 2);
            mesh._triangles.Add(startIndex + 3);
            mesh._triangles.Add(startIndex + 2);
            mesh._triangles.Add(startIndex + 1);
        }

        #endregion Mesh generation
    }
}
