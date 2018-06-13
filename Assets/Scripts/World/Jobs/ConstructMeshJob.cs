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

            var ao = CalculateAO(nextBlockPos, dir);
            var startIndex = mesh._vertices.Length;

            Quaternion rotation = Quaternion.LookRotation(vec);

            mesh._colors.Add(color * ao.x);
            mesh._colors.Add(color * ao.y);
            mesh._colors.Add(color * ao.z);
            mesh._colors.Add(color * ao.w);

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

        private Vector4 CalculateAO(Vector3Int nextBlockPos, DirectionsHelper.BlockDirectionFlag dir)
        {
            const int side1Mask = 0b100;
            const int side2Mask = 0b010;
            const int cornerMask = 0b001;

            int occl = 0;
            /*
             *  occl[0]   occl[1]    occl[2]
             *  -1,1       0,1        1,1
             *
             *          3--------4
             *  occl[7] |        |   occl[3]
             *  -1,0    |   0,0  |    1,0
             *          |        |
             *          |        |
             *          1--------2
             *  occl[6]   occl[5]    occl[4]
             *  -1,-1      0,-1       1,-1
             */

            int x = nextBlockPos.x + 1,
                y = nextBlockPos.y + 1,
                z = nextBlockPos.z + 1;
            //set occluders
            switch (dir)
            {
                case DirectionsHelper.BlockDirectionFlag.Up:
                    //sides
                    if (!chunkAndNeighboursVoxels[x - 1, y, z].type.IsAir())
                        occl |= 0b10000000;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z].type.IsAir())
                        occl |= 0b00001000;
                    if (!chunkAndNeighboursVoxels[x, y, z - 1].type.IsAir())
                        occl |= 0b00100000;
                    if (!chunkAndNeighboursVoxels[x, y, z + 1].type.IsAir())
                        occl |= 0b00000010;
                    //corners
                    if (!chunkAndNeighboursVoxels[x - 1, y, z - 1].type.IsAir())
                        occl |= 0b01000000;
                    if (!chunkAndNeighboursVoxels[x - 1, y, z + 1].type.IsAir())
                        occl |= 0b00000001;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z - 1].type.IsAir())
                        occl |= 0b00010000;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z + 1].type.IsAir())
                        occl |= 0b00000100;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Down:
                    //sides
                    if (!chunkAndNeighboursVoxels[x - 1, y, z].type.IsAir())
                        occl |= 0b10000000;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z].type.IsAir())
                        occl |= 0b00001000;
                    if (!chunkAndNeighboursVoxels[x, y, z - 1].type.IsAir())
                        occl |= 0b00100000;
                    if (!chunkAndNeighboursVoxels[x, y, z + 1].type.IsAir())
                        occl |= 0b00000010;
                    //corners
                    if (!chunkAndNeighboursVoxels[x - 1, y, z - 1].type.IsAir())
                        occl |= 0b01000000;
                    if (!chunkAndNeighboursVoxels[x - 1, y, z + 1].type.IsAir())
                        occl |= 0b00000001;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z - 1].type.IsAir())
                        occl |= 0b00010000;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z + 1].type.IsAir())
                        occl |= 0b00000100;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Left:
                    //sides
                    if (!chunkAndNeighboursVoxels[x, y - 1, z].type.IsAir())
                        occl |= 0b10000000;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z].type.IsAir())
                        occl |= 0b00001000;
                    if (!chunkAndNeighboursVoxels[x, y, z - 1].type.IsAir())
                        occl |= 0b00100000;
                    if (!chunkAndNeighboursVoxels[x, y, z + 1].type.IsAir())
                        occl |= 0b00000010;
                    //corners
                    if (!chunkAndNeighboursVoxels[x, y - 1, z - 1].type.IsAir())
                        occl |= 0b01000000;
                    if (!chunkAndNeighboursVoxels[x, y - 1, z + 1].type.IsAir())
                        occl |= 0b00000001;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z - 1].type.IsAir())
                        occl |= 0b00010000;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z + 1].type.IsAir())
                        occl |= 0b00000100;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Right:
                    //sides
                    if (!chunkAndNeighboursVoxels[x, y - 1, z].type.IsAir())
                        occl |= 0b10000000;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z].type.IsAir())
                        occl |= 0b00001000;
                    if (!chunkAndNeighboursVoxels[x, y, z - 1].type.IsAir())
                        occl |= 0b00100000;
                    if (!chunkAndNeighboursVoxels[x, y, z + 1].type.IsAir())
                        occl |= 0b00000010;
                    //corners
                    if (!chunkAndNeighboursVoxels[x, y - 1, z - 1].type.IsAir())
                        occl |= 0b01000000;
                    if (!chunkAndNeighboursVoxels[x, y - 1, z + 1].type.IsAir())
                        occl |= 0b00000001;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z - 1].type.IsAir())
                        occl |= 0b00010000;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z + 1].type.IsAir())
                        occl |= 0b00000100;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Back:
                    //sides
                    if (!chunkAndNeighboursVoxels[x - 1, y, z].type.IsAir())
                        occl |= 0b10000000;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z].type.IsAir())
                        occl |= 0b00001000;
                    if (!chunkAndNeighboursVoxels[x, y - 1, z].type.IsAir())
                        occl |= 0b00100000;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z].type.IsAir())
                        occl |= 0b00000010;
                    //corners
                    if (!chunkAndNeighboursVoxels[x - 1, y - 1, z].type.IsAir())
                        occl |= 0b01000000;
                    if (!chunkAndNeighboursVoxels[x - 1, y + 1, z].type.IsAir())
                        occl |= 0b00000001;
                    if (!chunkAndNeighboursVoxels[x + 1, y - 1, z].type.IsAir())
                        occl |= 0b00010000;
                    if (!chunkAndNeighboursVoxels[x + 1, y + 1, z].type.IsAir())
                        occl |= 0b00000100;
                    break;

                case DirectionsHelper.BlockDirectionFlag.Front:
                    //sides
                    if (!chunkAndNeighboursVoxels[x - 1, y, z].type.IsAir())
                        occl |= 0b10000000;
                    if (!chunkAndNeighboursVoxels[x + 1, y, z].type.IsAir())
                        occl |= 0b00001000;
                    if (!chunkAndNeighboursVoxels[x, y - 1, z].type.IsAir())
                        occl |= 0b00100000;
                    if (!chunkAndNeighboursVoxels[x, y + 1, z].type.IsAir())
                        occl |= 0b00000010;
                    //corners
                    if (!chunkAndNeighboursVoxels[x - 1, y - 1, z].type.IsAir())
                        occl |= 0b01000000;
                    if (!chunkAndNeighboursVoxels[x - 1, y + 1, z].type.IsAir())
                        occl |= 0b00000001;
                    if (!chunkAndNeighboursVoxels[x + 1, y - 1, z].type.IsAir())
                        occl |= 0b00010000;
                    if (!chunkAndNeighboursVoxels[x + 1, y + 1, z].type.IsAir())
                        occl |= 0b00000100;
                    break;
            }

            float vert1 = VertexAO(((occl & 0b10000000) >> 5) | ((occl & 0b00100000) >> 4) | ((occl & 0b01000000) >> 6));
            float vert2 = VertexAO(((occl & 0b00100000) >> 3) | ((occl & 0b00001000) >> 2) | ((occl & 0b00010000) >> 4));
            float vert3 = VertexAO(((occl & 0b10000000) >> 5) | ((occl & 0b00000010) >> 0) | ((occl & 0b00000001) >> 0));
            float vert4 = VertexAO(((occl & 0b00000010) << 1) | ((occl & 0b00001000) >> 2) | ((occl & 0b00000100) >> 2));

            return new Vector4(vert1, vert2, vert3, vert4);

            float VertexAO(int side12Corner)
            {
                int side1 = (side12Corner & side1Mask) >> 2;
                int side2 = (side12Corner & side2Mask) >> 1;
                int corner = side12Corner & cornerMask;
                if (side1 != 0 && side2 != 0)
                {
                    return 0;
                }
                return (3 - (side1 + side2 + corner)) / 3;
            }
        }

        #endregion Mesh generation
    }
}
