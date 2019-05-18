using Scripts.Help;
using Scripts.Help.DataContainers;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Scripts.World.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class ChunkMeshSystem : JobComponentSystem
    {
        private EntityQuery _chunksDirty;

        private class ChunkSystemBarrier : EntityCommandBufferSystem { }

        private EntityCommandBufferSystem _barrier;

        [BurstCompile]
        private struct CopyLightJob : IJob
        {
            [WriteOnly]
            public NativeArray3D<VoxelLightingLevel> LightingData;

            [ReadOnly]
            public BufferFromEntity<VoxelLightingLevel> ChunksLight;
            [ReadOnly]
            public ChunkNeighboursComponent Neighbours;
            [ReadOnly]
            public ComponentDataFromEntity<ChunkNeighboursComponent> AllChunksNeighbours;
            [ReadOnly]
            public Entity Chunk;

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

                for(int i = 0; i < 6; i++)
                {
                    var dir1 = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                }

                Copy6Sides(copyTo);

                Copy12Edges(copyTo);

                Copy8Vertices(copyTo);
            }

            private void Copy6Sides(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts._chunkSize;
                var neighb = Neighbours.Up;
                if(neighb != Entity.Null)
                {
                    var nextVox = ChunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, z + 1] = nextVox.AtGet(x, 0, z);
                }

                neighb = Neighbours.Down;
                if(neighb != Entity.Null)
                {
                    var nextVox = ChunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, z + 1] = nextVox.AtGet(x, sz - 1, z);
                }

                neighb = Neighbours.Left;
                if(neighb != Entity.Null)
                {
                    var nextVox = ChunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, z + 1] = nextVox.AtGet(sz - 1, y, z);
                }

                neighb = Neighbours.Right;
                if(neighb != Entity.Null)
                {
                    var nextVox = ChunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, z + 1] = nextVox.AtGet(0, y, z);
                }

                neighb = Neighbours.Backward;
                if(neighb != Entity.Null)
                {
                    var nextVox = ChunksLight[neighb];
                    for(int y = 0; y < sz; y++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, y + 1, 0] = nextVox.AtGet(x, y, sz - 1);
                }

                neighb = Neighbours.Forward;
                if(neighb != Entity.Null)
                {
                    var nextVox = ChunksLight[neighb];
                    for(int y = 0; y < sz; y++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, y + 1, sz + 1] = nextVox.AtGet(x, y, 0);
                }
            }

            private void Copy12Edges(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts._chunkSize;
                if(Neighbours.Up != Entity.Null)
                {
                    var neighb = AllChunksNeighbours[Neighbours.Up].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[sz + 1, sz + 1, z + 1] = nextVox.AtGet(0, 0, z);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Up].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[0, sz + 1, z + 1] = nextVox.AtGet(sz - 1, 0, z);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Up].Backward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, 0] = nextVox.AtGet(x, 0, sz - 1);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Up].Forward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, sz + 1] = nextVox.AtGet(x, 0, 0);
                    }
                }
                if(Neighbours.Down != Entity.Null)
                {
                    var neighb = AllChunksNeighbours[Neighbours.Down].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[sz + 1, 0, z + 1] = nextVox.AtGet(0, sz - 1, z);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Down].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[0, 0, z + 1] = nextVox.AtGet(sz - 1, sz - 1, z);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Down].Backward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, 0] = nextVox.AtGet(x, sz - 1, sz - 1);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Down].Forward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, sz + 1] = nextVox.AtGet(x, sz - 1, 0);
                    }
                }
                if(Neighbours.Forward != Entity.Null)
                {
                    var neighb = AllChunksNeighbours[Neighbours.Forward].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, sz + 1] = nextVox.AtGet(0, y, 0);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Forward].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, sz + 1] = nextVox.AtGet(sz - 1, y, 0);
                    }
                }
                if(Neighbours.Backward != Entity.Null)
                {
                    var neighb = AllChunksNeighbours[Neighbours.Backward].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, 0] = nextVox.AtGet(0, y, sz - 1);
                    }

                    neighb = AllChunksNeighbours[Neighbours.Backward].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = ChunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, 0] = nextVox.AtGet(sz - 1, y, sz - 1);
                    }
                }
            }

            private void Copy8Vertices(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxConsts._chunkSize;
                if(Neighbours.Up != Entity.Null)
                {
                    var neighb1 = AllChunksNeighbours[Neighbours.Up];

                    if(neighb1.Left != Entity.Null)
                    {
                        var neighb2 = AllChunksNeighbours[neighb1.Left].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[0, sz + 1, sz + 1] = nextVox.AtGet(sz - 1, 0, 0);
                        }

                        neighb2 = AllChunksNeighbours[neighb1.Left].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[0, sz + 1, 0] = nextVox.AtGet(sz - 1, 0, sz - 1);
                        }
                    }

                    if(neighb1.Right != Entity.Null)
                    {
                        var neighb2 = AllChunksNeighbours[neighb1.Right].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[sz + 1, sz + 1, sz + 1] = nextVox.AtGet(0, 0, 0);
                        }

                        neighb2 = AllChunksNeighbours[neighb1.Right].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[sz + 1, sz + 1, 0] = nextVox.AtGet(0, 0, sz - 1);
                        }
                    }
                }
                if(Neighbours.Down != Entity.Null)
                {
                    var neighb1 = AllChunksNeighbours[Neighbours.Down];

                    if(neighb1.Left != Entity.Null)
                    {
                        var neighb2 = AllChunksNeighbours[neighb1.Left].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[0, 0, sz + 1] = nextVox.AtGet(sz - 1, sz - 1, 0);
                        }

                        neighb2 = AllChunksNeighbours[neighb1.Left].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[0, 0, 0] = nextVox.AtGet(sz - 1, sz - 1, sz - 1);
                        }
                    }

                    if(neighb1.Right != Entity.Null)
                    {
                        var neighb2 = AllChunksNeighbours[neighb1.Right].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[sz + 1, 0, sz + 1] = nextVox.AtGet(0, sz - 1, 0);
                        }

                        neighb2 = AllChunksNeighbours[neighb1.Right].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = ChunksLight[neighb2];
                            copyTo[sz + 1, 0, 0] = nextVox.AtGet(0, sz - 1, sz - 1);
                        }
                    }
                }
            }

            #endregion Copying
        }

        [BurstCompile]
        private struct ConstructMeshJob : IJob
        {
            [WriteOnly]
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
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> VoxelsVisibleFaces;

            public void Execute()
            {
                var chunkBuffer = ChunkBufferEnt[Entity];
                for(int z = 0; z < VoxConsts._chunkSize; z++)
                {
                    for(int y = 0; y < VoxConsts._chunkSize; y++)
                    {
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
                }
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
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            [ReadOnly]
            public Entity chunk;

            [ReadOnly]
            public ChunkNeighboursComponent neighbours;

            [ReadOnly]
            public BufferFromEntity<Voxel> chunks;

            public void Execute(int index)
            {
                facesVisibleArr.At(index, out int x, out int y, out int z);

                var facesVisible = DirectionsHelper.BlockDirectionFlag.None;
                for(byte i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToInt3();
                    int xn = x + vec.x,
                        yn = y + vec.y,
                        zn = z + vec.z;
                    var chunkIndIn = chunks[chunk];
                    if(DirectionsHelper.WrapCoordsInChunk(ref xn, ref yn, ref zn) != DirectionsHelper.BlockDirectionFlag.None)
                    {
                        if(neighbours[dir] != Entity.Null)
                            chunkIndIn = chunks[neighbours[dir]];
                        else
                        {
                            facesVisible |= dir;
                            continue;
                        }
                    }

                    if(chunkIndIn.AtGet(xn, yn, zn).Type.IsEmpty())
                        facesVisible |= dir;
                }
                facesVisibleArr[x, y, z] = facesVisible;
            }
        }

        protected override void OnCreateManager()
        {
            _chunksDirty = GetEntityQuery(
                ComponentType.ReadOnly<RegularChunk>(),
                ComponentType.ReadOnly<ChunkDirtyComponent>(),
                ComponentType.ReadOnly<ChunkNeighboursComponent>(),
                ComponentType.ReadOnly<Voxel>(),
                ComponentType.ReadOnly<VoxelLightingLevel>());
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var commandBuffer = _barrier.CreateCommandBuffer();

            JobHandle handle;

            var dirty = _chunksDirty.ToComponentArray<RegularChunk>();
            using(var entities = _chunksDirty.ToEntityArray(Allocator.TempJob))
            using(var neig = _chunksDirty.ToComponentDataArray<ChunkNeighboursComponent>(Allocator.TempJob))
            using(var handles = new NativeList<JobHandle>(dirty.Length, Allocator.Temp))
            {
                for(int i = 0; i < dirty.Length; i++)
                {
                    handles.Add(CleanChunk(dirty[i], entities[i], neig[i], inputDeps));
                    commandBuffer.RemoveComponent<ChunkDirtyComponent>(entities[i]);
                    commandBuffer.AddComponent(entities[i], new ChunkNeedMeshApply());
                }
                handle = JobHandle.CombineDependencies(handles.AsArray());
            }
            return handle;
        }

        #region Chunk processing

        public JobHandle CleanChunk(RegularChunk chunk, Entity entity, ChunkNeighboursComponent neighb, JobHandle inputDeps)
        {
            var j1 = new RebuildChunkBlockVisibleFacesJob()
            {
                facesVisibleArr = new NativeArray3D<DirectionsHelper.BlockDirectionFlag>(
                    VoxConsts._chunkSize, VoxConsts._chunkSize, VoxConsts._chunkSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                chunk = entity,
                chunks = GetBufferFromEntity<Voxel>(true),
                neighbours = neighb,
            };
            var j2 = new CopyLightJob()
            {
                AllChunksNeighbours = GetComponentDataFromEntity<ChunkNeighboursComponent>(true),
                Chunk = entity,
                ChunksLight = GetBufferFromEntity<VoxelLightingLevel>(true),
                LightingData = new NativeArray3D<VoxelLightingLevel>(VoxConsts._chunkSize + 2, VoxConsts._chunkSize + 2, VoxConsts._chunkSize + 2, Allocator.TempJob),
                Neighbours = neighb,
            };
            var j3 = new ConstructMeshJob()
            {
                MeshData = chunk.MeshData,
                VoxelsVisibleFaces = j1.facesVisibleArr,
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
            return ((Vector3)pos * VoxConsts._chunkSize);
        }

        #endregion Helper methods
    }
}