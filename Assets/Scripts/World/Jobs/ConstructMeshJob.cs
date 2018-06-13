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
        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;

        [ReadOnly]
        public NativeArray3D<Voxel> chunkAndNeighboursVoxels;

        [ReadOnly]
        public NativeArray3D<VoxelLightingLevel> chunkAndNeighboursLighting;

        [WriteOnly]
        public NativeMeshData meshData;

        public void Execute()
        {
            for (int z = 0; z < VoxelWorldController._chunkSize; z++)
            {
                for (int y = 0; y < VoxelWorldController._chunkSize; y++)
                {
                    for (int x = 0; x < VoxelWorldController._chunkSize; x++)
                    {
                        if (chunkAndNeighboursVoxels[x + 1, y + 1, z + 1].type != VoxelType.Air)
                        {
                            var col = chunkAndNeighboursVoxels[x + 1, y + 1, z + 1].ToColor();
                            //var faces = CalculateVisibleFaces(x, y, z);
                            var faces = voxelsVisibleFaces[x, y, z];
                            CreateCube(meshData, new Vector3(x, y, z) * VoxelWorldController._blockSize, faces, col, new Vector3Int(x, y, z));
                        }
                    }
                }
            }
        }

        private DirectionsHelper.BlockDirectionFlag CalculateVisibleFaces(int x, int y, int z)
        {
            DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
            for (byte i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                Vector3Int vec = dir.ToVec();

                if (chunkAndNeighboursVoxels[x + vec.x + 1, y + vec.y + 1, z + vec.z + 1].type.IsAir())
                    facesVisible |= dir;
            }
            return facesVisible;
        }

        #region Mesh generation

        private void CreateCube(NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Color color, Vector3Int blockPos)
        {
            for (int i = 0; i < 6; i++)
            {
                var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                if ((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                    CreateFace(mesh, pos, curr, color, blockPos);
            }
        }

        private void CreateFace(NativeMeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Color color, Vector3Int blockPos)
        {
            var vec = dir.ToVec();
            var nextBlockPos = blockPos + vec;

            byte light = CalculateLightForAFace(nextBlockPos, dir);
            //var occlusion = (new Vector4(2, 2, 2, 2) - CalculateAmbientOcclusion(nextBlockPos, dir)) / 2;

            color *= (float)(light + 8) / 32;

            var startIndex = mesh._vertices.Length;

            Quaternion rotation = Quaternion.LookRotation(vec);

            mesh._colors.Add(color);
            mesh._colors.Add(color);
            mesh._colors.Add(color);
            mesh._colors.Add(color);

            mesh._uv.Add(new Vector2(0, 0));
            mesh._uv.Add(new Vector2(1, 0));
            mesh._uv.Add(new Vector2(0, 1));
            mesh._uv.Add(new Vector2(1, 1));

            mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);

            Vector3Int normal = dir.ToVec();

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

        private byte CalculateLightForAFace(Vector3Int nextBlockPos, DirectionsHelper.BlockDirectionFlag dir)
        {
            return chunkAndNeighboursLighting[nextBlockPos.x + 1, nextBlockPos.y + 1, nextBlockPos.z + 1]._level;
        }

        private Vector4 CalculateAmbientOcclusion(Vector3Int nextBlockPos, DirectionsHelper.BlockDirectionFlag dir)
        {
            var occluders = DirectionsHelper.BlockDirectionFlag.None;
            for (int i = 0; i < 6; i++)
            {
                var dirDir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
            }
            float vert0 = 0;
            float vert1 = 0;
            float vert2 = 0;
            float vert3 = 0;

            switch (dir)//kill me pls
            {
                case DirectionsHelper.BlockDirectionFlag.Up:
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert0 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert0 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert1 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert1 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert2 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert2 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert3 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert3 += 1;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Down:
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert0 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert0 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert1 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert1 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert2 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert2 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert3 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert3 += 1;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Left:
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert0 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert0 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert1 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert1 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert2 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert2 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert3 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert3 += 1;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Right:
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert0 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert0 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Back) != 0)
                        vert1 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert1 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert2 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert2 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert3 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Front) != 0)
                        vert3 += 1;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Back:
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert0 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert0 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert1 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert1 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert2 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert2 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert3 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert3 += 1;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Front:
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert0 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert0 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                        vert1 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert1 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Left) != 0)
                        vert2 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert2 += 1;

                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Up) != 0)
                        vert3 += 1;
                    if ((occluders & DirectionsHelper.BlockDirectionFlag.Right) != 0)
                        vert3 += 1;
                    break;
            }
            return new Vector4(vert0, vert1, vert2, vert3);
        }

        #endregion Mesh generation
    }
}
