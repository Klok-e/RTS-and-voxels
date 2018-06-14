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
                            var faces = CalculateVisibleFaces(x, y, z);
                            //var faces = voxelsVisibleFaces[x, y, z];
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
                Vector3Int vec = dir.ToVecInt();

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
            var vec = dir.ToVecInt();

            byte light = CalculateLightForAFace(blockPos, dir);
            //var occlusion = (new Vector4(2, 2, 2, 2) - CalculateAmbientOcclusion(nextBlockPos, dir)) / 2;

            color *= (float)(light + 8) / 32;

            var ao = CalculateAO(blockPos, dir, out bool isFlipped);
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

            mesh._uv2.Add(new Vector2(ao.x, 0));
            mesh._uv2.Add(new Vector2(ao.y, 0));
            mesh._uv2.Add(new Vector2(ao.z, 0));
            mesh._uv2.Add(new Vector2(ao.w, 0));

            mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);

            Vector3Int normal = dir.ToVecInt();

            mesh._normals.Add(normal);
            mesh._normals.Add(normal);
            mesh._normals.Add(normal);
            mesh._normals.Add(normal);

            if (isFlipped)
            {
                mesh._triangles.Add(startIndex + 1);
                mesh._triangles.Add(startIndex + 3);
                mesh._triangles.Add(startIndex + 0);
                mesh._triangles.Add(startIndex + 2);
                mesh._triangles.Add(startIndex + 0);
                mesh._triangles.Add(startIndex + 3);
            }
            else
            {
                mesh._triangles.Add(startIndex + 0);
                mesh._triangles.Add(startIndex + 1);
                mesh._triangles.Add(startIndex + 2);
                mesh._triangles.Add(startIndex + 3);
                mesh._triangles.Add(startIndex + 2);
                mesh._triangles.Add(startIndex + 1);
            }
        }

        private byte CalculateLightForAFace(Vector3Int blockPos, DirectionsHelper.BlockDirectionFlag dir)
        {
            blockPos += dir.ToVecInt();
            return chunkAndNeighboursLighting[blockPos.x + 1, blockPos.y + 1, blockPos.z + 1]._level;
        }

        private Vector4 CalculateAO(Vector3Int blockPos, DirectionsHelper.BlockDirectionFlag dir, out bool isFlipped)
        {
            const int side1Mask = 0b100;
            const int side2Mask = 0b010;
            const int cornerMask = 0b001;

            int occl = 0;
            /*
             *  occl[0]   occl[1]    occl[2]
             *  -1,1      0,1       1,1
             *
             *          2--------3
             *  occl[7] |        |   occl[3]
             *  -1,0    |   0,0  |    1,0
             *          |        |
             *          |        |
             *          0--------1
             *  occl[6]   occl[5]    occl[4]
             *  -1,-1       0,-1        1,-1
             */
            var vec = dir.ToVecInt();
            blockPos += new Vector3Int(1, 1, 1);

            var rotationToDir = Quaternion.LookRotation(vec, new Vector3(0, 1, 0));
            //set occluders
            var left = (rotationToDir * new Vector3(-1, 0, 1)).ToInt();
            var right = (rotationToDir * new Vector3(1, 0, 1)).ToInt();
            var front = (rotationToDir * new Vector3(0, 1, 1)).ToInt();
            var back = (rotationToDir * new Vector3(0, -1, 1)).ToInt();

            var frontLeft = (rotationToDir * new Vector3(-1, 1, 1)).ToInt();
            var frontRight = (rotationToDir * new Vector3(1, 1, 1)).ToInt();
            var backLeft = (rotationToDir * new Vector3(-1, -1, 1)).ToInt();
            var backRight = (rotationToDir * new Vector3(1, -1, 1)).ToInt();

            left += blockPos;
            right += blockPos;
            front += blockPos;
            back += blockPos;

            frontLeft += blockPos;
            frontRight += blockPos;
            backLeft += blockPos;
            backRight += blockPos;

            //sides
            if (!chunkAndNeighboursVoxels[left.x, left.y, left.z].type.IsAir())
                occl |= 0b10000000;
            if (!chunkAndNeighboursVoxels[right.x, right.y, right.z].type.IsAir())
                occl |= 0b00001000;
            if (!chunkAndNeighboursVoxels[front.x, front.y, front.z].type.IsAir())
                occl |= 0b00000010;
            if (!chunkAndNeighboursVoxels[back.x, back.y, back.z].type.IsAir())
                occl |= 0b00100000;
            //corners
            if (!chunkAndNeighboursVoxels[frontLeft.x, frontLeft.y, frontLeft.z].type.IsAir())
                occl |= 0b00000001;
            if (!chunkAndNeighboursVoxels[backLeft.x, backLeft.y, backLeft.z].type.IsAir())
                occl |= 0b01000000;
            if (!chunkAndNeighboursVoxels[frontRight.x, frontRight.y, frontRight.z].type.IsAir())
                occl |= 0b00000100;
            if (!chunkAndNeighboursVoxels[backRight.x, backRight.y, backRight.z].type.IsAir())
                occl |= 0b00010000;

            float vert1 = VertexAO(((occl & 0b10000000) >> 5) | ((occl & 0b00100000) >> 4) | ((occl & 0b01000000) >> 6));
            float vert2 = VertexAO(((occl & 0b00100000) >> 3) | ((occl & 0b00001000) >> 2) | ((occl & 0b00010000) >> 4));
            float vert3 = VertexAO(((occl & 0b10000000) >> 5) | ((occl & 0b00000010) >> 0) | ((occl & 0b00000001) >> 0));
            float vert4 = VertexAO(((occl & 0b00000010) << 1) | ((occl & 0b00001000) >> 2) | ((occl & 0b00000100) >> 2));

            //from here: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
            if (vert1 + vert4 > vert3 + vert2)
                isFlipped = true;
            else
                isFlipped = false;

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
