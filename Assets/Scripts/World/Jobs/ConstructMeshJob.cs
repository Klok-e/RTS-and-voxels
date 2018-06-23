﻿using Scripts.Help;
using Scripts.World;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World.Jobs
{
    [BurstCompile]
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
                            //var faces = CalculateVisibleFaces(x, y, z);
                            var faces = voxelsVisibleFaces[x, y, z];
                            CreateCube(meshData, new Vector3(x, y, z) * VoxelWorldController._blockSize, faces, new Vector3Int(x, y, z), chunkAndNeighboursVoxels[x + 1, y + 1, z + 1].type);
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

        private void CreateCube(NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Vector3Int blockPos, VoxelType voxelType)
        {
            for (int i = 0; i < 6; i++)
            {
                var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                if ((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                    CreateFace(mesh, pos, curr, blockPos, voxelType);
            }
        }

        private void CreateFace(NativeMeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Vector3Int blockPos, VoxelType voxelType)
        {
            var vec = dir.ToVecInt();

            var light = CalculateLightForAFaceSmooth(blockPos, dir, out bool isFlipped);

            //var ao = CalculateAO(blockPos, dir, out isFlipped);
            var startIndex = mesh._vertices.Length;

            Quaternion rotation = Quaternion.LookRotation(vec);

            mesh._colors.Add(new Color(1, 1, 1) * light.x);
            mesh._colors.Add(new Color(1, 1, 1) * light.y);
            mesh._colors.Add(new Color(1, 1, 1) * light.z);
            mesh._colors.Add(new Color(1, 1, 1) * light.w);

            mesh._uv.Add(new Vector2(0, 0));
            mesh._uv.Add(new Vector2(1, 0));
            mesh._uv.Add(new Vector2(0, 1));
            mesh._uv.Add(new Vector2(1, 1));

            switch (voxelType)
            {
                case VoxelType.Dirt:
                    mesh._uv2.Add(new Vector2(1, 0));
                    mesh._uv2.Add(new Vector2(1, 0));
                    mesh._uv2.Add(new Vector2(1, 0));
                    mesh._uv2.Add(new Vector2(1, 0));
                    break;

                case VoxelType.Grass:
                    if (dir == DirectionsHelper.BlockDirectionFlag.Up)
                    {
                        mesh._uv2.Add(new Vector2(1, 1));
                        mesh._uv2.Add(new Vector2(1, 1));
                        mesh._uv2.Add(new Vector2(1, 1));
                        mesh._uv2.Add(new Vector2(1, 1));
                    }
                    else
                    {
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                    }
                    break;
            }

            mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);
            mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorldController._blockSize)) + vertOffset);

            Vector3Int normal = dir.ToVecInt();

            mesh._normals.Add(normal);
            mesh._normals.Add(normal);
            mesh._normals.Add(normal);
            mesh._normals.Add(normal);

            if (isFlipped)
            {
                mesh._triangles.Add(startIndex + 2);
                mesh._triangles.Add(startIndex + 3);
                mesh._triangles.Add(startIndex + 0);
                mesh._triangles.Add(startIndex + 1);
                mesh._triangles.Add(startIndex + 0);
                mesh._triangles.Add(startIndex + 3);
            }
            else
            {
                mesh._triangles.Add(startIndex + 0);
                mesh._triangles.Add(startIndex + 2);
                mesh._triangles.Add(startIndex + 1);
                mesh._triangles.Add(startIndex + 3);
                mesh._triangles.Add(startIndex + 1);
                mesh._triangles.Add(startIndex + 2);
            }
        }

        private Vector4 CalculateLightForAFaceSimple(Vector3Int blockPos, DirectionsHelper.BlockDirectionFlag dir, out bool isFlipped)
        {
            blockPos += dir.ToVecInt();
            float light = chunkAndNeighboursLighting[blockPos.x + 1, blockPos.y + 1, blockPos.z + 1].RegularLight;

            isFlipped = false;

            return new Vector4(light, light, light, light) / 15f;
        }

        private Vector4 CalculateLightForAFaceSmooth(Vector3Int blockPos, DirectionsHelper.BlockDirectionFlag dir, out bool isFlipped)
        {
            var vec = dir.ToVecFloat();
            blockPos += new Vector3Int(1, 1, 1);

            /*
             *  -1,1       0,1        1,1
             *
             *          2--------3
             *          |        |
             *  -1,0    |  0,0   |    1,0
             *          |        |
             *          |        |
             *          0--------1
             *
             *  -1,-1      0,-1       1,-1
             */

            var rotationToDir = Quaternion.LookRotation(vec);

            //set occluders
            var centerInd = (rotationToDir * new Vector3(0, 0, 1)).ToInt();
            var leftInd = (rotationToDir * new Vector3(-1, 0, 1)).ToInt();
            var rightInd = (rotationToDir * new Vector3(1, 0, 1)).ToInt();
            var frontInd = (rotationToDir * new Vector3(0, -1, 1)).ToInt();
            var backInd = (rotationToDir * new Vector3(0, 1, 1)).ToInt();
            var frontLeftInd = (rotationToDir * new Vector3(-1, -1, 1)).ToInt();
            var frontRightInd = (rotationToDir * new Vector3(1, -1, 1)).ToInt();
            var backLeftInd = (rotationToDir * new Vector3(-1, 1, 1)).ToInt();
            var backRightInd = (rotationToDir * new Vector3(1, 1, 1)).ToInt();

            centerInd += blockPos;
            leftInd += blockPos;
            rightInd += blockPos;
            frontInd += blockPos;
            backInd += blockPos;
            frontLeftInd += blockPos;
            frontRightInd += blockPos;
            backLeftInd += blockPos;
            backRightInd += blockPos;

            var center = chunkAndNeighboursLighting[centerInd.x, centerInd.y, centerInd.z];
            var left = chunkAndNeighboursLighting[leftInd.x, leftInd.y, leftInd.z];
            var right = chunkAndNeighboursLighting[rightInd.x, rightInd.y, rightInd.z];
            var front = chunkAndNeighboursLighting[frontInd.x, frontInd.y, frontInd.z];
            var back = chunkAndNeighboursLighting[backInd.x, backInd.y, backInd.z];
            var frontLeft = chunkAndNeighboursLighting[frontLeftInd.x, frontLeftInd.y, frontLeftInd.z];
            var frontRight = chunkAndNeighboursLighting[frontRightInd.x, frontRightInd.y, frontRightInd.z];
            var backLeft = chunkAndNeighboursLighting[backLeftInd.x, backLeftInd.y, backLeftInd.z];
            var backRight = chunkAndNeighboursLighting[backRightInd.x, backRightInd.y, backRightInd.z];

            int centerVal = Math.Max(center.RegularLight, center.Sunlight);
            int leftVal = Math.Max(left.RegularLight, left.Sunlight);
            int rightVal = Math.Max(right.RegularLight, right.Sunlight);
            int frontVal = Math.Max(front.RegularLight, front.Sunlight);
            int backVal = Math.Max(back.RegularLight, back.Sunlight);
            int frontLeftVal = Math.Max(frontLeft.RegularLight, frontLeft.Sunlight);
            int frontRightVal = Math.Max(frontRight.RegularLight, frontRight.Sunlight);
            int backLeftVal = Math.Max(backLeft.RegularLight, backLeft.Sunlight);
            int backRightVal = Math.Max(backRight.RegularLight, backRight.Sunlight);

            float vert1 = VertexLight(centerVal, leftVal, backVal, backLeftVal);
            float vert2 = VertexLight(centerVal, rightVal, backVal, backRightVal);
            float vert3 = VertexLight(centerVal, leftVal, frontVal, frontLeftVal);
            float vert4 = VertexLight(centerVal, rightVal, frontVal, frontRightVal);

            //source: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
            if (vert1 + vert4 > vert2 + vert3)
                isFlipped = true;
            else
                isFlipped = false;

            return new Vector4(vert1, vert2, vert3, vert4);
        }

        private float VertexLight(int center, int side1, int side2, int corner)
        {
            return (center + side1 + side2 + corner) / 4f / 15f;
        }

        private Vector4 CalculateAO(Vector3Int blockPos, DirectionsHelper.BlockDirectionFlag dir, out bool isFlipped)
        {
            var vec = dir.ToVecFloat();
            blockPos += new Vector3Int(1, 1, 1);

            var rotationToDir = Quaternion.LookRotation(vec);

            //set occluders
            var leftInd = (rotationToDir * new Vector3(-1, 0, 1)).ToInt();
            var rightInd = (rotationToDir * new Vector3(1, 0, 1)).ToInt();
            var frontInd = (rotationToDir * new Vector3(0, -1, 1)).ToInt();
            var backInd = (rotationToDir * new Vector3(0, 1, 1)).ToInt();
            var frontLeftInd = (rotationToDir * new Vector3(-1, -1, 1)).ToInt();
            var frontRightInd = (rotationToDir * new Vector3(1, -1, 1)).ToInt();
            var backLeftInd = (rotationToDir * new Vector3(-1, 1, 1)).ToInt();
            var backRightInd = (rotationToDir * new Vector3(1, 1, 1)).ToInt();

            leftInd += blockPos;
            rightInd += blockPos;
            frontInd += blockPos;
            backInd += blockPos;
            frontLeftInd += blockPos;
            frontRightInd += blockPos;
            backLeftInd += blockPos;
            backRightInd += blockPos;

            int left = 0;
            int right = 0;
            int front = 0;
            int back = 0;
            int frontLeft = 0;
            int frontRight = 0;
            int backLeft = 0;
            int backRight = 0;

            if (!chunkAndNeighboursVoxels[frontLeftInd.x, frontLeftInd.y, frontLeftInd.z].type.IsAir())
                frontLeft = 1;
            if (!chunkAndNeighboursVoxels[frontInd.x, frontInd.y, frontInd.z].type.IsAir())
                front = 1;
            if (!chunkAndNeighboursVoxels[frontRightInd.x, frontRightInd.y, frontRightInd.z].type.IsAir())
                frontRight = 1;
            if (!chunkAndNeighboursVoxels[rightInd.x, rightInd.y, rightInd.z].type.IsAir())
                right = 1;
            if (!chunkAndNeighboursVoxels[backRightInd.x, backRightInd.y, backRightInd.z].type.IsAir())
                backRight = 1;
            if (!chunkAndNeighboursVoxels[backInd.x, backInd.y, backInd.z].type.IsAir())
                back = 1;
            if (!chunkAndNeighboursVoxels[backLeftInd.x, backLeftInd.y, backLeftInd.z].type.IsAir())
                backLeft = 1;
            if (!chunkAndNeighboursVoxels[leftInd.x, leftInd.y, leftInd.z].type.IsAir())
                left = 1;

            float vert1 = VertexAO(left, back, backLeft);
            float vert2 = VertexAO(right, back, backRight);
            float vert3 = VertexAO(left, front, frontLeft);
            float vert4 = VertexAO(right, front, frontRight);

            //source: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
            if (vert1 + vert4 > vert2 + vert3)
                isFlipped = true;
            else
                isFlipped = false;

            return new Vector4(vert1, vert2, vert3, vert4);

            float VertexAO(int side1, int side2, int corner)
            {
                if (side1 == 1 && side2 == 1)
                {
                    return 0;
                }
                return (3 - (side1 + side2 + corner)) / 3f;
            }
        }

        #endregion Mesh generation
    }
}
