﻿using Scripts.Help;
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
        public NativeArray3D<VoxelLightingLevel> voxelLightingLevels;

        [ReadOnly]
        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;

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
                    col *= ((float)voxelLightingLevels[x, y, z]._level / 32);

                    CreateCube(ref meshData, new Vector3(x, y, z) * VoxelWorldController._blockSize, voxelsVisibleFaces[x, y, z], col);
                }
            }
        }

        #region Mesh generation

        private static void CreateCube(ref NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Color color)
        {
            for (int i = 0; i < 6; i++)
            {
                var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                if ((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                    CreateFace(ref mesh, pos, curr, color);
            }
        }

        private static void CreateFace(ref NativeMeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Color color)
        {
            var startIndex = mesh._vertices.Length;

            Quaternion rotation = Quaternion.identity;

            switch (dir)
            {
                case DirectionsHelper.BlockDirectionFlag.Left: rotation = Quaternion.LookRotation(Vector3.left); break;
                case DirectionsHelper.BlockDirectionFlag.Right: rotation = Quaternion.LookRotation(Vector3.right); break;
                case DirectionsHelper.BlockDirectionFlag.Down: rotation = Quaternion.LookRotation(Vector3.down); break;
                case DirectionsHelper.BlockDirectionFlag.Up: rotation = Quaternion.LookRotation(Vector3.up); break;
                case DirectionsHelper.BlockDirectionFlag.Back: rotation = Quaternion.LookRotation(Vector3.back); break;
                case DirectionsHelper.BlockDirectionFlag.Front: rotation = Quaternion.LookRotation(Vector3.forward); break;
                default: throw new Exception();
            }

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
