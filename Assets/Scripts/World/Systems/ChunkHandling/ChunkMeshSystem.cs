using Scripts.Help;
using Scripts.Help.DataContainers;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Scripts.World.Systems.Regions;
using Scripts.World.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Direction = Scripts.Help.DirectionsHelper.BlockDirectionFlag;

namespace Scripts.World.Systems
{
    // TODO: Refactor to JobForeach
    public class ChunkMeshSystem : JobComponentSystem
    {
        [BurstCompile]
        private struct CopyLightJob : IJob
        {
            [WriteOnly]
            public NativeArray3D<VoxelLightingLevel> LightingData;

            [ReadOnly]
            public BufferFromEntity<VoxelLightingLevel> ChunksLight;

            [ReadOnly]
            public Entity Chunk;

            [ReadOnly]
            public ChunkPosComponent ChunkPos;

            [ReadOnly]
            public NativeHashMap<int3, Entity> PosToEntity;

            public void Execute()
            {
                CopyNeighboursLight(LightingData);
            }

            #region Copying
            private void CopyNeighboursLight(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                var voxLightBuff = ChunksLight[Chunk];
                for(int z = 1; z < VoxConsts._chunkSize + 1; z++)
                    for(int y = 1; y < VoxConsts._chunkSize + 1; y++)
                        for(int x = 1; x < VoxConsts._chunkSize + 1; x++)
                        {
                            copyTo[x, y, z] = voxLightBuff.AtGet(x - 1, y - 1, z - 1);
                        }

                Copy6Sides(copyTo);

                Copy12Edges(copyTo);

                Copy8Vertices(copyTo);
            }

            private void Copy6Sides(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts._chunkSize;
                var neighbDir = Direction.Up;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out var nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, z + 1] = nextVox.AtGet(x, 0, z);
                }

                neighbDir = Direction.Down;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, z + 1] = nextVox.AtGet(x, sz - 1, z);
                }

                neighbDir = Direction.Left;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, z + 1] = nextVox.AtGet(sz - 1, y, z);
                }

                neighbDir = Direction.Right;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, z + 1] = nextVox.AtGet(0, y, z);
                }

                neighbDir = Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int y = 0; y < sz; y++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, y + 1, 0] = nextVox.AtGet(x, y, sz - 1);
                }

                neighbDir = Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int y = 0; y < sz; y++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, y + 1, sz + 1] = nextVox.AtGet(x, y, 0);
                }
            }

            private void Copy12Edges(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts._chunkSize;

                var neighbDir = Direction.Up | Direction.Right;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out var nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        copyTo[sz + 1, sz + 1, z + 1] = nextVox.AtGet(0, 0, z);
                }

                neighbDir = Direction.Up | Direction.Left;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        copyTo[0, sz + 1, z + 1] = nextVox.AtGet(sz - 1, 0, z);
                }

                neighbDir = Direction.Up | Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int x = 0; x < sz; x++)
                        copyTo[x + 1, sz + 1, 0] = nextVox.AtGet(x, 0, sz - 1);
                }

                neighbDir = Direction.Up | Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int x = 0; x < sz; x++)
                        copyTo[x + 1, sz + 1, sz + 1] = nextVox.AtGet(x, 0, 0);
                }

                neighbDir = Direction.Down | Direction.Right;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        copyTo[sz + 1, 0, z + 1] = nextVox.AtGet(0, sz - 1, z);
                }

                neighbDir = Direction.Down | Direction.Left;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int z = 0; z < sz; z++)
                        copyTo[0, 0, z + 1] = nextVox.AtGet(sz - 1, sz - 1, z);
                }

                neighbDir = Direction.Down | Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int x = 0; x < sz; x++)
                        copyTo[x + 1, 0, 0] = nextVox.AtGet(x, sz - 1, sz - 1);
                }

                neighbDir = Direction.Down | Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int x = 0; x < sz; x++)
                        copyTo[x + 1, 0, sz + 1] = nextVox.AtGet(x, sz - 1, 0);
                }

                neighbDir = Direction.Forward | Direction.Right;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int y = 0; y < sz; y++)
                        copyTo[sz + 1, y + 1, sz + 1] = nextVox.AtGet(0, y, 0);
                }

                neighbDir = Direction.Forward | Direction.Left;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int y = 0; y < sz; y++)
                        copyTo[0, y + 1, sz + 1] = nextVox.AtGet(sz - 1, y, 0);
                }

                neighbDir = Direction.Backward | Direction.Right;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int y = 0; y < sz; y++)
                        copyTo[sz + 1, y + 1, 0] = nextVox.AtGet(0, y, sz - 1);
                }

                neighbDir = Direction.Backward | Direction.Left;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    for(int y = 0; y < sz; y++)
                        copyTo[0, y + 1, 0] = nextVox.AtGet(sz - 1, y, sz - 1);
                }
            }

            private void Copy8Vertices(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts._chunkSize;

                var neighbDir = Direction.Up | Direction.Left | Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out var nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[0, sz + 1, sz + 1] = nextVox.AtGet(sz - 1, 0, 0);
                }


                neighbDir = Direction.Up | Direction.Left | Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[0, sz + 1, 0] = nextVox.AtGet(sz - 1, 0, sz - 1);
                }

                neighbDir = Direction.Up | Direction.Right | Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[sz + 1, sz + 1, sz + 1] = nextVox.AtGet(0, 0, 0);
                }

                neighbDir = Direction.Up | Direction.Right | Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[sz + 1, sz + 1, 0] = nextVox.AtGet(0, 0, sz - 1);
                }

                neighbDir = Direction.Down | Direction.Right | Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[0, 0, sz + 1] = nextVox.AtGet(sz - 1, sz - 1, 0);
                }

                neighbDir = Direction.Down | Direction.Left | Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[0, 0, 0] = nextVox.AtGet(sz - 1, sz - 1, sz - 1);
                }

                neighbDir = Direction.Down | Direction.Right | Direction.Forward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[sz + 1, 0, sz + 1] = nextVox.AtGet(0, sz - 1, 0);
                }

                neighbDir = Direction.Down | Direction.Right | Direction.Backward;
                if(PosToEntity.TryGetValue(ChunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = ChunksLight[nextEnt];
                    copyTo[sz + 1, 0, 0] = nextVox.AtGet(0, sz - 1, sz - 1);
                }
            }

            #endregion Copying
        }

        [BurstCompile]
        private struct ConstructMeshJob : IJob
        {
            public NativeMeshData MeshData;

            [ReadOnly]
            public BufferFromEntity<Voxel> ChunkBufferEnt;

            [ReadOnly]
            public Entity Entity;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<VoxelLightingLevel> LightingData;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<Direction> VoxelsVisibleFaces;

            public void Execute()
            {
                var chunkBuffer = ChunkBufferEnt[Entity];
                for(int z = 0; z < VoxConsts._chunkSize; z++)
                    for(int y = 0; y < VoxConsts._chunkSize; y++)
                        for(int x = 0; x < VoxConsts._chunkSize; x++)
                        {
                            var vox = chunkBuffer.AtGet(x, y, z).Type;
                            if(vox != VoxelType.Empty)
                            {
                                var faces = VoxelsVisibleFaces[x, y, z];
                                CreateCube(MeshData, new Vector3(x, y, z) * VoxConsts._blockSize, faces, new Vector3Int(x, y, z), vox);
                            }
                        }
            }

            #region Mesh generation

            private void CreateCube(NativeMeshData mesh, Vector3 pos, Direction facesVisible, Vector3Int blockPos, VoxelType voxelType)
            {
                for(int i = 0; i < 6; i++)
                {
                    var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    if((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                        CreateFace(mesh, pos, curr, blockPos, voxelType);
                }
            }

            private void CreateFace(NativeMeshData mesh, Vector3 vertOffset, Direction dir, Vector3Int blockPos, VoxelType voxelType)
            {
                var normal = dir.ToVecFloat();

                var light = CalculateLightForAFaceSmooth(blockPos, normal, out bool isFlipped);

                //var ao = CalculateAO(blockPos, dir, out isFlipped);
                var startIndex = mesh._vertices.Length;

                var rotation = Quaternion.LookRotation(normal);

                mesh._colors.Add(new Color(1, 1, 1) * light.x);
                mesh._colors.Add(new Color(1, 1, 1) * light.y);
                mesh._colors.Add(new Color(1, 1, 1) * light.z);
                mesh._colors.Add(new Color(1, 1, 1) * light.w);

                mesh._uv.Add(new Vector2(0, 0));
                mesh._uv.Add(new Vector2(1, 0));
                mesh._uv.Add(new Vector2(0, 1));
                mesh._uv.Add(new Vector2(1, 1));

                voxelType.Mesh(dir, mesh);

                mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxConsts._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxConsts._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxConsts._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxConsts._blockSize)) + vertOffset);

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

            private Vector4 CalculateLightForAFaceSmooth(Vector3Int blockPos, Vector3 normal, out bool isFlipped)
            {
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

                var rotationToDir = Quaternion.LookRotation(normal);

                //set occluders
                var centerInd = (rotationToDir * new Vector3(0, 0, 1)).ToVecInt();
                var leftInd = (rotationToDir * new Vector3(-1, 0, 1)).ToVecInt();
                var rightInd = (rotationToDir * new Vector3(1, 0, 1)).ToVecInt();
                var forwardInd = (rotationToDir * new Vector3(0, -1, 1)).ToVecInt();
                var backwardInd = (rotationToDir * new Vector3(0, 1, 1)).ToVecInt();
                var forwardLeftInd = (rotationToDir * new Vector3(-1, -1, 1)).ToVecInt();
                var forwardRightInd = (rotationToDir * new Vector3(1, -1, 1)).ToVecInt();
                var backwardLeftInd = (rotationToDir * new Vector3(-1, 1, 1)).ToVecInt();
                var backwardRightInd = (rotationToDir * new Vector3(1, 1, 1)).ToVecInt();

                centerInd += blockPos;
                leftInd += blockPos;
                rightInd += blockPos;
                forwardInd += blockPos;
                backwardInd += blockPos;
                forwardLeftInd += blockPos;
                forwardRightInd += blockPos;
                backwardLeftInd += blockPos;
                backwardRightInd += blockPos;

                var center = LightingData[centerInd.x, centerInd.y, centerInd.z];
                var left = LightingData[leftInd.x, leftInd.y, leftInd.z];
                var right = LightingData[rightInd.x, rightInd.y, rightInd.z];
                var front = LightingData[forwardInd.x, forwardInd.y, forwardInd.z];
                var back = LightingData[backwardInd.x, backwardInd.y, backwardInd.z];
                var frontLeft = LightingData[forwardLeftInd.x, forwardLeftInd.y, forwardLeftInd.z];
                var frontRight = LightingData[forwardRightInd.x, forwardRightInd.y, forwardRightInd.z];
                var backLeft = LightingData[backwardLeftInd.x, backwardLeftInd.y, backwardLeftInd.z];
                var backRight = LightingData[backwardRightInd.x, backwardRightInd.y, backwardRightInd.z];

                int centerVal = math.max(center.RegularLight, center.Sunlight);
                int leftVal = math.max(left.RegularLight, left.Sunlight);
                int rightVal = math.max(right.RegularLight, right.Sunlight);
                int frontVal = math.max(front.RegularLight, front.Sunlight);
                int backVal = math.max(back.RegularLight, back.Sunlight);
                int frontLeftVal = math.max(frontLeft.RegularLight, frontLeft.Sunlight);
                int frontRightVal = math.max(frontRight.RegularLight, frontRight.Sunlight);
                int backLeftVal = math.max(backLeft.RegularLight, backLeft.Sunlight);
                int backRightVal = math.max(backRight.RegularLight, backRight.Sunlight);

                //int centerVal = center.RegularLight;
                //int leftVal = left.RegularLight;
                //int rightVal = right.RegularLight;
                //int frontVal = front.RegularLight;
                //int backVal = back.RegularLight;
                //int frontLeftVal = frontLeft.RegularLight;
                //int frontRightVal = frontRight.RegularLight;
                //int backLeftVal = backLeft.RegularLight;
                //int backRightVal = backRight.RegularLight;

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

            #endregion Mesh generation
        }

        [BurstCompile]
        private struct RebuildChunkBlockVisibleFacesJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray3D<Direction> FacesVisibleArr;

            [ReadOnly]
            public Entity Chunk;

            [ReadOnly]
            public NativeHashMap<int3, Entity> PosToEntity;

            [ReadOnly]
            public ChunkPosComponent Pos;

            [ReadOnly]
            public BufferFromEntity<Voxel> Chunks;

            public void Execute(int index)
            {
                FacesVisibleArr.At(index, out int x, out int y, out int z);

                var facesVisible = Direction.None;
                for(byte i = 0; i < 6; i++)
                {
                    var dir = (Direction)(1 << i);
                    var vec = dir.ToInt3();
                    int xn = x + vec.x,
                        yn = y + vec.y,
                        zn = z + vec.z;
                    var chunkIndIn = Chunks[Chunk];
                    if(DirectionsHelper.WrapCoordsInChunk(ref xn, ref yn, ref zn) != Direction.None)
                    {
                        if(PosToEntity.TryGetValue(Pos.Pos + vec, out var ent))
                            chunkIndIn = Chunks[ent];
                        else
                            facesVisible |= dir;
                    }

                    if(chunkIndIn.AtGet(xn, yn, zn).Type.IsEmpty())
                        facesVisible |= dir;
                }
                FacesVisibleArr[x, y, z] = facesVisible;
            }
        }

        private EntityQuery _chunksDirty;

        private EntityCommandBufferSystem _barrier;

        private RegionLoadUnloadSystem _chunkCreationSystem;

        protected override void OnCreateManager()
        {
            _chunksDirty = GetEntityQuery(
                ComponentType.ReadOnly<ChunkDirtyComponent>(),
                ComponentType.ReadOnly<Voxel>(),
                ComponentType.ReadOnly<VoxelLightingLevel>(),
                ComponentType.ReadOnly<ChunkPosComponent>());

            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _chunkCreationSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var commandBuffer = _barrier.CreateCommandBuffer();
            var dict = _chunkCreationSystem.PosToChunk;

            JobHandle handle;
            using(var entities = _chunksDirty.ToEntityArray(Allocator.TempJob))
            using(var neig = _chunksDirty.ToComponentDataArray<ChunkPosComponent>(Allocator.TempJob))
            using(var handles = new NativeList<JobHandle>(entities.Length, Allocator.Temp))
            {
                for(int i = 0; i < entities.Length; i++)
                {
                    handles.Add(CleanChunk(dict[neig[i].Pos], entities[i], neig[i], inputDeps));
                    commandBuffer.RemoveComponent<ChunkDirtyComponent>(entities[i]);
                    commandBuffer.AddComponent(entities[i], new ChunkNeedMeshApply());
                }
                handle = JobHandle.CombineDependencies(handles.AsArray());
            }

            _barrier.AddJobHandleForProducer(handle);

            return handle;
        }

        #region Chunk processing

        public JobHandle CleanChunk(RegularChunk chunk, Entity entity, ChunkPosComponent pos, JobHandle inputDeps)
        {
            //Debug.Log(chunk.name);
            var j1 = new RebuildChunkBlockVisibleFacesJob()
            {
                FacesVisibleArr = new NativeArray3D<Direction>(
                    VoxConsts._chunkSize, VoxConsts._chunkSize, VoxConsts._chunkSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                Chunk = entity,
                Chunks = GetBufferFromEntity<Voxel>(true),
                Pos = pos,
                PosToEntity = _chunkCreationSystem.PosToEntity,
            };
            var j2 = new CopyLightJob()
            {
                Chunk = entity,
                ChunksLight = GetBufferFromEntity<VoxelLightingLevel>(true),
                LightingData = new NativeArray3D<VoxelLightingLevel>(VoxConsts._chunkSize + 2, VoxConsts._chunkSize + 2, VoxConsts._chunkSize + 2, Allocator.TempJob),
                ChunkPos = pos,
                PosToEntity = _chunkCreationSystem.PosToEntity,
            };
            var j3 = new ConstructMeshJob()
            {
                MeshData = chunk.MeshData,
                VoxelsVisibleFaces = j1.FacesVisibleArr,
                ChunkBufferEnt = GetBufferFromEntity<Voxel>(true),
                LightingData = j2.LightingData,
                Entity = entity,
            };

            return j3.Schedule(JobHandle.CombineDependencies(
                j2.Schedule(inputDeps),
                j1.Schedule(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize, 1024, inputDeps)));
        }

        #endregion Chunk processing

        #region Helper methods

        public static void ChunkVoxelCoordinates(Vector3 worldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            worldPos /= VoxConsts._chunkSize;
            chunkPos = ((worldPos - (Vector3.one * (VoxConsts._chunkSize / 2))) / VoxConsts._chunkSize).ToVecInt();
            voxelPos = (worldPos - chunkPos * VoxConsts._chunkSize).ToVecInt();
        }

        public static void ChunkVoxelCoordinates(Vector3Int voxelWorldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            chunkPos = ((voxelWorldPos - (Vector3.one * (VoxConsts._chunkSize / 2))) / VoxConsts._chunkSize).ToVecInt();
            voxelPos = (voxelWorldPos - chunkPos * VoxConsts._chunkSize);
        }

        public static Vector3Int WorldPosToVoxelPos(Vector3 pos)
        {
            return (pos / VoxConsts._chunkSize).ToVecInt();
        }

        public static Vector3 VoxelPosToWorldPos(Vector3Int pos)
        {
            return (Vector3)pos * VoxConsts._chunkSize;
        }

        #endregion Helper methods
    }
}