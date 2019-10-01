using Help;
using Help.DataContainers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using World.Components;
using World.DynamicBuffers;
using World.Systems.Regions;
using World.Utils;
using Direction = Help.DirectionsHelper.BlockDirectionFlag;

namespace World.Systems.ChunkHandling
{
    // TODO: Refactor to JobForeach
    public class ChunkMeshSystem : JobComponentSystem
    {
        private EntityCommandBufferSystem _barrier;

        private RegionLoadUnloadSystem _chunkCreationSystem;

        private EntityQuery _chunksDirty;

        protected override void OnCreateManager()
        {
            _chunksDirty = GetEntityQuery(
                ComponentType.ReadOnly<ChunkDirtyComponent>(),
                ComponentType.ReadOnly<Voxel>(),
                ComponentType.ReadOnly<VoxelLightingLevel>(),
                ComponentType.ReadOnly<ChunkPosComponent>());

            _barrier             = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _chunkCreationSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var commandBuffer = _barrier.CreateCommandBuffer();
            var dict          = _chunkCreationSystem.PosToChunk;

            JobHandle handle;
            using (var entities = _chunksDirty.ToEntityArray(Allocator.TempJob))
            using (var neig = _chunksDirty.ToComponentDataArray<ChunkPosComponent>(Allocator.TempJob))
            using (var handles = new NativeList<JobHandle>(entities.Length, Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
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

        private JobHandle CleanChunk(RegularChunk chunk, Entity entity, ChunkPosComponent pos, JobHandle inputDeps)
        {
            //Debug.Log(chunk.name);
            var j1 = new RebuildChunkBlockVisibleFacesJob
            {
                facesVisibleArr = new NativeArray3D<Direction>(
                    VoxConsts.ChunkSize, VoxConsts.ChunkSize, VoxConsts.ChunkSize, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory),
                chunk       = entity,
                chunks      = GetBufferFromEntity<Voxel>(true),
                pos         = pos,
                posToEntity = _chunkCreationSystem.PosToChunkEntity
            };
            var j2 = new CopyLightJob
            {
                chunk       = entity,
                chunksLight = GetBufferFromEntity<VoxelLightingLevel>(true),
                lightingData = new NativeArray3D<VoxelLightingLevel>(VoxConsts.ChunkSize + 2, VoxConsts.ChunkSize + 2,
                    VoxConsts.ChunkSize                                                  + 2, Allocator.TempJob),
                chunkPos    = pos,
                posToEntity = _chunkCreationSystem.PosToChunkEntity
            };
            var j3 = new ConstructMeshJob
            {
                meshData           = chunk.MeshData,
                voxelsVisibleFaces = j1.facesVisibleArr,
                chunkBufferEnt     = GetBufferFromEntity<Voxel>(true),
                lightingData       = j2.lightingData,
                entity             = entity
            };

            return j3.Schedule(JobHandle.CombineDependencies(
                j2.Schedule(inputDeps),
                j1.Schedule(VoxConsts.ChunkSize * VoxConsts.ChunkSize * VoxConsts.ChunkSize, 1024, inputDeps)));
        }

        #endregion Chunk processing

        [BurstCompile]
        private struct CopyLightJob : IJob
        {
            [WriteOnly]
            public NativeArray3D<VoxelLightingLevel> lightingData;

            [ReadOnly]
            public BufferFromEntity<VoxelLightingLevel> chunksLight;

            [ReadOnly]
            public Entity chunk;

            [ReadOnly]
            public ChunkPosComponent chunkPos;

            [ReadOnly]
            public NativeHashMap<int3, Entity> posToEntity;

            public void Execute()
            {
                CopyNeighboursLight(lightingData);
            }

            #region Copying

            private void CopyNeighboursLight(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                var voxLightBuff = chunksLight[chunk];
                for (int z = 1; z < VoxConsts.ChunkSize + 1; z++)
                for (int y = 1; y < VoxConsts.ChunkSize + 1; y++)
                for (int x = 1; x < VoxConsts.ChunkSize + 1; x++)
                    copyTo[x, y, z] = voxLightBuff.AtGet(x - 1, y - 1, z - 1);

                Copy6Sides(copyTo);

                Copy12Edges(copyTo);

                Copy8Vertices(copyTo);
            }

            private void Copy6Sides(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz        = VoxConsts.ChunkSize;
                var       neighbDir = Direction.Up;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out var nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, sz + 1, z + 1] = nextVox.AtGet(x, 0, z);
                }

                neighbDir = Direction.Down;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, 0, z + 1] = nextVox.AtGet(x, sz - 1, z);
                }

                neighbDir = Direction.Left;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                    for (int y = 0; y < sz; y++)
                        copyTo[0, y + 1, z + 1] = nextVox.AtGet(sz - 1, y, z);
                }

                neighbDir = Direction.Right;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                    for (int y = 0; y < sz; y++)
                        copyTo[sz + 1, y + 1, z + 1] = nextVox.AtGet(0, y, z);
                }

                neighbDir = Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int y = 0; y < sz; y++)
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, y + 1, 0] = nextVox.AtGet(x, y, sz - 1);
                }

                neighbDir = Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int y = 0; y < sz; y++)
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, y + 1, sz + 1] = nextVox.AtGet(x, y, 0);
                }
            }

            private void Copy12Edges(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts.ChunkSize;

                var neighbDir = Direction.Up | Direction.Right;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out var nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                        copyTo[sz + 1, sz + 1, z + 1] = nextVox.AtGet(0, 0, z);
                }

                neighbDir = Direction.Up | Direction.Left;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                        copyTo[0, sz + 1, z + 1] = nextVox.AtGet(sz - 1, 0, z);
                }

                neighbDir = Direction.Up | Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, sz + 1, 0] = nextVox.AtGet(x, 0, sz - 1);
                }

                neighbDir = Direction.Up | Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, sz + 1, sz + 1] = nextVox.AtGet(x, 0, 0);
                }

                neighbDir = Direction.Down | Direction.Right;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                        copyTo[sz + 1, 0, z + 1] = nextVox.AtGet(0, sz - 1, z);
                }

                neighbDir = Direction.Down | Direction.Left;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int z = 0; z < sz; z++)
                        copyTo[0, 0, z + 1] = nextVox.AtGet(sz - 1, sz - 1, z);
                }

                neighbDir = Direction.Down | Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, 0, 0] = nextVox.AtGet(x, sz - 1, sz - 1);
                }

                neighbDir = Direction.Down | Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int x = 0; x < sz; x++)
                        copyTo[x + 1, 0, sz + 1] = nextVox.AtGet(x, sz - 1, 0);
                }

                neighbDir = Direction.Forward | Direction.Right;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int y = 0; y < sz; y++)
                        copyTo[sz + 1, y + 1, sz + 1] = nextVox.AtGet(0, y, 0);
                }

                neighbDir = Direction.Forward | Direction.Left;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int y = 0; y < sz; y++)
                        copyTo[0, y + 1, sz + 1] = nextVox.AtGet(sz - 1, y, 0);
                }

                neighbDir = Direction.Backward | Direction.Right;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int y = 0; y < sz; y++)
                        copyTo[sz + 1, y + 1, 0] = nextVox.AtGet(0, y, sz - 1);
                }

                neighbDir = Direction.Backward | Direction.Left;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    for (int y = 0; y < sz; y++)
                        copyTo[0, y + 1, 0] = nextVox.AtGet(sz - 1, y, sz - 1);
                }
            }

            private void Copy8Vertices(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts.ChunkSize;

                var neighbDir = Direction.Up | Direction.Left | Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out var nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[0, sz + 1, sz + 1] = nextVox.AtGet(sz - 1, 0, 0);
                }


                neighbDir = Direction.Up | Direction.Left | Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[0, sz + 1, 0] = nextVox.AtGet(sz - 1, 0, sz - 1);
                }

                neighbDir = Direction.Up | Direction.Right | Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[sz + 1, sz + 1, sz + 1] = nextVox.AtGet(0, 0, 0);
                }

                neighbDir = Direction.Up | Direction.Right | Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[sz + 1, sz + 1, 0] = nextVox.AtGet(0, 0, sz - 1);
                }

                neighbDir = Direction.Down | Direction.Right | Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[0, 0, sz + 1] = nextVox.AtGet(sz - 1, sz - 1, 0);
                }

                neighbDir = Direction.Down | Direction.Left | Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[0, 0, 0] = nextVox.AtGet(sz - 1, sz - 1, sz - 1);
                }

                neighbDir = Direction.Down | Direction.Right | Direction.Forward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[sz + 1, 0, sz + 1] = nextVox.AtGet(0, sz - 1, 0);
                }

                neighbDir = Direction.Down | Direction.Right | Direction.Backward;
                if (posToEntity.TryGetValue(chunkPos.Pos + neighbDir.ToInt3(), out nextEnt))
                {
                    var nextVox = chunksLight[nextEnt];
                    copyTo[sz + 1, 0, 0] = nextVox.AtGet(0, sz - 1, sz - 1);
                }
            }

            #endregion Copying
        }

        [BurstCompile]
        private struct ConstructMeshJob : IJob
        {
            public NativeMeshData meshData;

            [ReadOnly]
            public BufferFromEntity<Voxel> chunkBufferEnt;

            [ReadOnly]
            public Entity entity;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<VoxelLightingLevel> lightingData;

            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<Direction> voxelsVisibleFaces;

            public void Execute()
            {
                var chunkBuffer = chunkBufferEnt[entity];
                for (int z = 0; z < VoxConsts.ChunkSize; z++)
                for (int y = 0; y < VoxConsts.ChunkSize; y++)
                for (int x = 0; x < VoxConsts.ChunkSize; x++)
                {
                    var vox = chunkBuffer.AtGet(x, y, z).type;
                    if (vox != VoxelType.Empty)
                    {
                        var faces = voxelsVisibleFaces[x, y, z];
                        CreateCube(meshData,         new Vector3(x, y, z) * VoxConsts.BlockSize, faces,
                            new Vector3Int(x, y, z), vox);
                    }
                }
            }

            #region Mesh generation

            private void CreateCube(NativeMeshData mesh, Vector3 pos, Direction facesVisible, Vector3Int blockPos,
                                    VoxelType      voxelType)
            {
                for (int i = 0; i < 6; i++)
                {
                    var curr = (Direction) (1 << i);
                    if ((curr & facesVisible) != 0) //0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                        CreateFace(mesh, pos, curr, blockPos, voxelType);
                }
            }

            private void CreateFace(NativeMeshData mesh, Vector3 vertOffset, Direction dir, Vector3Int blockPos,
                                    VoxelType      voxelType)
            {
                var normal = dir.ToVecFloat();

                var light = CalculateLightForAFaceSmooth(blockPos, normal, out bool isFlipped);

                //var ao = CalculateAO(blockPos, dir, out isFlipped);
                int startIndex = mesh._vertices.Length;

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

                // ReSharper disable Unity.InefficientMultiplicationOrder
                mesh._vertices.Add(rotation * (new Vector3(-.5f, .5f,  .5f) * VoxConsts.BlockSize) + vertOffset);
                mesh._vertices.Add(rotation * (new Vector3(.5f,  .5f,  .5f) * VoxConsts.BlockSize) + vertOffset);
                mesh._vertices.Add(rotation * (new Vector3(-.5f, -.5f, .5f) * VoxConsts.BlockSize) + vertOffset);
                mesh._vertices.Add(rotation * (new Vector3(.5f,  -.5f, .5f) * VoxConsts.BlockSize) + vertOffset);
                // ReSharper restore Unity.InefficientMultiplicationOrder

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
                var centerInd        = (rotationToDir * new Vector3(0, 0, 1)).ToVecInt();
                var leftInd          = (rotationToDir * new Vector3(-1, 0, 1)).ToVecInt();
                var rightInd         = (rotationToDir * new Vector3(1, 0, 1)).ToVecInt();
                var forwardInd       = (rotationToDir * new Vector3(0, -1, 1)).ToVecInt();
                var backwardInd      = (rotationToDir * new Vector3(0, 1, 1)).ToVecInt();
                var forwardLeftInd   = (rotationToDir * new Vector3(-1, -1, 1)).ToVecInt();
                var forwardRightInd  = (rotationToDir * new Vector3(1, -1, 1)).ToVecInt();
                var backwardLeftInd  = (rotationToDir * new Vector3(-1, 1, 1)).ToVecInt();
                var backwardRightInd = (rotationToDir * new Vector3(1, 1, 1)).ToVecInt();

                centerInd        += blockPos;
                leftInd          += blockPos;
                rightInd         += blockPos;
                forwardInd       += blockPos;
                backwardInd      += blockPos;
                forwardLeftInd   += blockPos;
                forwardRightInd  += blockPos;
                backwardLeftInd  += blockPos;
                backwardRightInd += blockPos;

                var center     = lightingData[centerInd.x, centerInd.y, centerInd.z];
                var left       = lightingData[leftInd.x, leftInd.y, leftInd.z];
                var right      = lightingData[rightInd.x, rightInd.y, rightInd.z];
                var front      = lightingData[forwardInd.x, forwardInd.y, forwardInd.z];
                var back       = lightingData[backwardInd.x, backwardInd.y, backwardInd.z];
                var frontLeft  = lightingData[forwardLeftInd.x, forwardLeftInd.y, forwardLeftInd.z];
                var frontRight = lightingData[forwardRightInd.x, forwardRightInd.y, forwardRightInd.z];
                var backLeft   = lightingData[backwardLeftInd.x, backwardLeftInd.y, backwardLeftInd.z];
                var backRight  = lightingData[backwardRightInd.x, backwardRightInd.y, backwardRightInd.z];

                int centerVal     = math.max(center.RegularLight,     center.Sunlight);
                int leftVal       = math.max(left.RegularLight,       left.Sunlight);
                int rightVal      = math.max(right.RegularLight,      right.Sunlight);
                int frontVal      = math.max(front.RegularLight,      front.Sunlight);
                int backVal       = math.max(back.RegularLight,       back.Sunlight);
                int frontLeftVal  = math.max(frontLeft.RegularLight,  frontLeft.Sunlight);
                int frontRightVal = math.max(frontRight.RegularLight, frontRight.Sunlight);
                int backLeftVal   = math.max(backLeft.RegularLight,   backLeft.Sunlight);
                int backRightVal  = math.max(backRight.RegularLight,  backRight.Sunlight);

                //int centerVal = center.RegularLight;
                //int leftVal = left.RegularLight;
                //int rightVal = right.RegularLight;
                //int frontVal = front.RegularLight;
                //int backVal = back.RegularLight;
                //int frontLeftVal = frontLeft.RegularLight;
                //int frontRightVal = frontRight.RegularLight;
                //int backLeftVal = backLeft.RegularLight;
                //int backRightVal = backRight.RegularLight;

                float vert1 = VertexLight(centerVal, leftVal,  backVal,  backLeftVal);
                float vert2 = VertexLight(centerVal, rightVal, backVal,  backRightVal);
                float vert3 = VertexLight(centerVal, leftVal,  frontVal, frontLeftVal);
                float vert4 = VertexLight(centerVal, rightVal, frontVal, frontRightVal);

                //source: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
                isFlipped = vert1 + vert4 > vert2 + vert3;

                return new Vector4(vert1, vert2, vert3, vert4);
            }

            private static float VertexLight(int center, int side1, int side2, int corner)
            {
                return (center + side1 + side2 + corner) / 4f / 15f;
            }

            #endregion Mesh generation
        }

        [BurstCompile]
        private struct RebuildChunkBlockVisibleFacesJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray3D<Direction> facesVisibleArr;

            [ReadOnly]
            public Entity chunk;

            [ReadOnly]
            public NativeHashMap<int3, Entity> posToEntity;

            [ReadOnly]
            public ChunkPosComponent pos;

            [ReadOnly]
            public BufferFromEntity<Voxel> chunks;

            public void Execute(int index)
            {
                facesVisibleArr.At(index, out int x, out int y, out int z);

                var facesVisible = Direction.None;
                for (byte i = 0; i < 6; i++)
                {
                    var dir = (Direction) (1 << i);
                    var vec = dir.ToInt3();
                    int xn = x + vec.x,
                        yn = y + vec.y,
                        zn = z + vec.z;
                    var chunkIndIn = chunks[chunk];
                    if (DirectionsHelper.WrapCoordsInChunk(ref xn, ref yn, ref zn) != Direction.None)
                    {
                        if (posToEntity.TryGetValue(pos.Pos + vec, out var ent))
                            chunkIndIn = chunks[ent];
                        else
                            facesVisible |= dir;
                    }

                    if (chunkIndIn.AtGet(xn, yn, zn).type.IsEmpty())
                        facesVisible |= dir;
                }

                facesVisibleArr[x, y, z] = facesVisible;
            }
        }

        #region Helper methods

        public static void ChunkVoxelCoordinates(Vector3 worldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            worldPos /= VoxConsts.ChunkSize;
            // ReSharper disable once PossibleLossOfFraction
            chunkPos =  ((worldPos - Vector3.one * (VoxConsts.ChunkSize / 2)) / VoxConsts.ChunkSize).ToVecInt();
            voxelPos =  (worldPos - chunkPos * VoxConsts.ChunkSize).ToVecInt();
        }

        public static void ChunkVoxelCoordinates(Vector3Int     voxelWorldPos, out Vector3Int chunkPos,
                                                 out Vector3Int voxelPos)
        {
            // ReSharper disable once PossibleLossOfFraction
            chunkPos = ((voxelWorldPos - Vector3.one * (VoxConsts.ChunkSize / 2)) / VoxConsts.ChunkSize).ToVecInt();
            voxelPos = voxelWorldPos - chunkPos * VoxConsts.ChunkSize;
        }

        public static Vector3Int WorldPosToVoxelPos(Vector3 pos)
        {
            return (pos / VoxConsts.ChunkSize).ToVecInt();
        }

        public static Vector3 VoxelPosToWorldPos(Vector3Int pos)
        {
            return (Vector3) pos * VoxConsts.ChunkSize;
        }

        #endregion Helper methods
    }
}