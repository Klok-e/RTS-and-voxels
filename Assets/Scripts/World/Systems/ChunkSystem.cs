using Scripts.Help;
using Scripts.Help.DataContainers;
using Scripts.World.Components;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class ChunkSystem : ComponentSystem
    {
        private ComponentGroup _chunksDirty;

        [BurstCompile]
        public struct ConstructMeshJob : IJob
        {
            [ReadOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<Voxel> chunkAndNeighboursVoxels;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<VoxelLightingLevel> chunkAndNeighboursLighting;

            [WriteOnly]
            public NativeMeshData meshData;

            public void Execute()
            {
                for(int z = 0; z < VoxelWorld._chunkSize; z++)
                {
                    for(int y = 0; y < VoxelWorld._chunkSize; y++)
                    {
                        for(int x = 0; x < VoxelWorld._chunkSize; x++)
                        {
                            if(chunkAndNeighboursVoxels[x + 1, y + 1, z + 1].type != VoxelType.Empty)
                            {
                                //var faces = CalculateVisibleFaces(x, y, z);
                                var faces = voxelsVisibleFaces[x, y, z];
                                CreateCube(meshData, new Vector3(x, y, z) * VoxelWorld._blockSize, faces, new Vector3Int(x, y, z), chunkAndNeighboursVoxels[x + 1, y + 1, z + 1].type);
                            }
                        }
                    }
                }
            }

            private DirectionsHelper.BlockDirectionFlag CalculateVisibleFaces(int x, int y, int z)
            {
                DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
                for(byte i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    Vector3Int vec = dir.ToVecInt();

                    if(chunkAndNeighboursVoxels[x + vec.x + 1, y + vec.y + 1, z + vec.z + 1].type.IsAir())
                        facesVisible |= dir;
                }
                return facesVisible;
            }

            #region Mesh generation

            private void CreateCube(NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Vector3Int blockPos, VoxelType voxelType)
            {
                for(int i = 0; i < 6; i++)
                {
                    var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    if((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
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

                switch(voxelType)
                {
                    case VoxelType.Dirt:
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        break;

                    case VoxelType.Grass:
                        if(dir == DirectionsHelper.BlockDirectionFlag.Up)
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

                mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorld._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorld._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorld._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorld._blockSize)) + vertOffset);

                Vector3Int normal = dir.ToVecInt();

                mesh._normals.Add(normal);
                mesh._normals.Add(normal);
                mesh._normals.Add(normal);
                mesh._normals.Add(normal);

                if(isFlipped)
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

                int centerVal = math.max(center.RegularLight, center.Sunlight);
                int leftVal = math.max(left.RegularLight, left.Sunlight);
                int rightVal = math.max(right.RegularLight, right.Sunlight);
                int frontVal = math.max(front.RegularLight, front.Sunlight);
                int backVal = math.max(back.RegularLight, back.Sunlight);
                int frontLeftVal = math.max(frontLeft.RegularLight, frontLeft.Sunlight);
                int frontRightVal = math.max(frontRight.RegularLight, frontRight.Sunlight);
                int backLeftVal = math.max(backLeft.RegularLight, backLeft.Sunlight);
                int backRightVal = math.max(backRight.RegularLight, backRight.Sunlight);

                float vert1 = VertexLight(centerVal, leftVal, backVal, backLeftVal);
                float vert2 = VertexLight(centerVal, rightVal, backVal, backRightVal);
                float vert3 = VertexLight(centerVal, leftVal, frontVal, frontLeftVal);
                float vert4 = VertexLight(centerVal, rightVal, frontVal, frontRightVal);

                //source: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
                if(vert1 + vert4 > vert2 + vert3)
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

                if(!chunkAndNeighboursVoxels[frontLeftInd.x, frontLeftInd.y, frontLeftInd.z].type.IsAir())
                    frontLeft = 1;
                if(!chunkAndNeighboursVoxels[frontInd.x, frontInd.y, frontInd.z].type.IsAir())
                    front = 1;
                if(!chunkAndNeighboursVoxels[frontRightInd.x, frontRightInd.y, frontRightInd.z].type.IsAir())
                    frontRight = 1;
                if(!chunkAndNeighboursVoxels[rightInd.x, rightInd.y, rightInd.z].type.IsAir())
                    right = 1;
                if(!chunkAndNeighboursVoxels[backRightInd.x, backRightInd.y, backRightInd.z].type.IsAir())
                    backRight = 1;
                if(!chunkAndNeighboursVoxels[backInd.x, backInd.y, backInd.z].type.IsAir())
                    back = 1;
                if(!chunkAndNeighboursVoxels[backLeftInd.x, backLeftInd.y, backLeftInd.z].type.IsAir())
                    backLeft = 1;
                if(!chunkAndNeighboursVoxels[leftInd.x, leftInd.y, leftInd.z].type.IsAir())
                    left = 1;

                float vert1 = VertexAO(left, back, backLeft);
                float vert2 = VertexAO(right, back, backRight);
                float vert3 = VertexAO(left, front, frontLeft);
                float vert4 = VertexAO(right, front, frontRight);

                //source: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
                if(vert1 + vert4 > vert2 + vert3)
                    isFlipped = true;
                else
                    isFlipped = false;

                return new Vector4(vert1, vert2, vert3, vert4);

                float VertexAO(int side1, int side2, int corner)
                {
                    if(side1 == 1 && side2 == 1)
                    {
                        return 0;
                    }
                    return (3 - (side1 + side2 + corner)) / 3f;
                }
            }

            #endregion Mesh generation
        }

        [BurstCompile]
        public struct RebuildChunkBlockVisibleFacesJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<Voxel> boxThatContainsChunkAndAllNeighboursBorders;

            public void Execute(int index)
            {
                facesVisibleArr.At(index, out int x, out int y, out int z);

                DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
                for(byte i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    Vector3Int vec = dir.ToVecInt();

                    if(boxThatContainsChunkAndAllNeighboursBorders[x + vec.x + 1, y + vec.y + 1, z + vec.z + 1].type.IsAir())
                        facesVisible |= dir;
                }
                facesVisibleArr[x, y, z] = facesVisible;
            }
        }

        protected override void OnCreateManager()
        {
            _chunksDirty = EntityManager.CreateComponentGroup(typeof(RegularChunk), typeof(ChunkDirtyComponent));
            Initialize();
        }

        protected override void OnDestroyManager()
        {
        }

        protected override void OnUpdate()
        {
            var dirty = _chunksDirty.GetComponentArray<RegularChunk>();
            var entities = _chunksDirty.GetEntityArray();
            for(int i = 0; i < dirty.Length; i++)
            {
                CleanChunk(dirty[i]);
                PostUpdateCommands.RemoveComponent<ChunkDirtyComponent>(entities[i]);
            }

            //EmptyQuerySetLightQueue();
            //EmptyQuerySetVoxelQueue();

            //DepropagateRegularLightSynchronously();
            //DepropagateSunlightSynchronously();

            //PropagateRegularLightSynchronously();
            //PropagateSunlightSynchronously();
        }

        /// <summary>
        /// Only uneven amount or else SetVoxel won't work at all
        /// </summary>
        public const int _chunkSize = 33;

        /// <summary>
        /// Size of a voxel
        /// </summary>
        public const float _blockSize = 0.5f;

        #region Visible in inspector

        #endregion Visible in inspector

        #region Private

        private Texture2DArray _textureArray;

        private Queue<VoxelLightPropagationData> _toPropagateRegularLight;
        private Queue<VoxelLightPropagationData> _toRemoveRegularLight;
        private Queue<VoxelLightPropagationData> _toPropagateSunlight;
        private Queue<VoxelLightPropagationData> _toRemoveSunlight;

        private MassJobThing _massJobThing;

        #endregion Private

        public void Initialize()
        {
            _massJobThing = new MassJobThing();

            _toPropagateRegularLight = new Queue<VoxelLightPropagationData>();
            _toRemoveRegularLight = new Queue<VoxelLightPropagationData>();
            _toPropagateSunlight = new Queue<VoxelLightPropagationData>();
            _toRemoveSunlight = new Queue<VoxelLightPropagationData>();

            //CreateStartingLevels(0, _up, _down);
        }

        #region Chunk processing

        public void CleanChunk(RegularChunk chunk, JobHandle dependency = default)
        {
            var hn1 = new RebuildChunkBlockVisibleFacesJob()
            {
                facesVisibleArr = chunk.VoxelsVisibleFaces,

                boxThatContainsChunkAndAllNeighboursBorders = CopyGivenAndNeighbourBordersVoxels(chunk),
            }.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, dependency);

            var hn2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                chunkAndNeighboursVoxels = CopyGivenAndNeighbourBordersVoxels(chunk),
                chunkAndNeighboursLighting = CopyGivenAndNeighbourBordersLighting(chunk),

                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
            }.Schedule(hn1);

            hn2.Complete();
        }

        #endregion Chunk processing

        #region LightPropagation


        #endregion LightPropagation

        #region Copy voxel data


        #endregion Copy voxel data

        #region Get something methods

        #endregion Get something methods

        #region Add to queues methods

        private void SetToPropagateAllLight(VoxelLightPropagationData data)
        {
            _toPropagateRegularLight.Enqueue(data);
            _toPropagateSunlight.Enqueue(data);
        }

        private void SetToRemoveAllLight(VoxelLightPropagationData data)
        {
            _toRemoveRegularLight.Enqueue(data);
            _toRemoveSunlight.Enqueue(data);
        }

        private void SetToPropagateRegularLight(VoxelLightPropagationData data)
        {
            _toPropagateRegularLight.Enqueue(data);
        }

        private void SetToRemoveRegularLight(VoxelLightPropagationData data)
        {
            _toRemoveRegularLight.Enqueue(data);
        }

        private void SetToPropagateSunlight(VoxelLightPropagationData data)
        {
            _toPropagateSunlight.Enqueue(data);
        }

        private void SetToRemoveSunlight(VoxelLightPropagationData data)
        {
            _toRemoveSunlight.Enqueue(data);
        }

        #endregion Add to queues methods

        #region Resolve change voxel data queries


        #endregion Resolve change voxel data queries

        #region Voxel editing


        #endregion Voxel editing

        #region Level generation


        #endregion Level generation

        #region Helper methods

        public static void ChunkVoxelCoordinates(Vector3 worldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            worldPos /= _blockSize;
            chunkPos = ((worldPos - (Vector3.one * (_chunkSize / 2))) / _chunkSize).ToInt();
            voxelPos = (worldPos - chunkPos * _chunkSize).ToInt();
        }

        public static void ChunkVoxelCoordinates(Vector3Int voxelWorldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            chunkPos = ((voxelWorldPos - (Vector3.one * (_chunkSize / 2))) / _chunkSize).ToInt();
            voxelPos = (voxelWorldPos - chunkPos * _chunkSize);
        }

        public static Vector3Int WorldPosToVoxelPos(Vector3 pos)
        {
            return (pos / _blockSize).ToInt();
        }

        public static Vector3 VoxelPosToWorldPos(Vector3Int pos)
        {
            return ((Vector3)pos * _blockSize);
        }

        #endregion Helper methods
    }
}