using Scripts.Help;
using Scripts.Help.DataContainers;
using Scripts.World.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class ChunkSystem : JobComponentSystem
    {
        private ComponentGroup _chunksDirty;

        [UpdateAfter(typeof(ChunkSystem))]
        private class ChunkSystemBarrier : BarrierSystem { }
        [Inject]
        private ChunkSystemBarrier _barrier;

        [BurstCompile]
        public struct ConstructMeshJob : IJob
        {
            [DeallocateOnJobCompletion]
            [ReadOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;
            [ReadOnly]
            public Entity chunk;
            [ReadOnly]
            public ChunkNeighboursComponent neighbours;
            [ReadOnly]
            public ComponentDataFromEntity<ChunkNeighboursComponent> allChunksNeighbours;
            [ReadOnly]
            public BufferFromEntity<Voxel> chunksVox;
            [ReadOnly]
            public BufferFromEntity<VoxelLightingLevel> chunksLight;

            [WriteOnly]
            public NativeMeshData meshData;

            [DeallocateOnJobCompletion]
            public NativeArray3D<VoxelLightingLevel> lightingData;

            public void Execute()
            {
                var chunkBuffer = chunksVox[chunk];
                CopyNeighboursLight(lightingData);
                for(int z = 0; z < VoxelWorld._chunkSize; z++)
                {
                    for(int y = 0; y < VoxelWorld._chunkSize; y++)
                    {
                        for(int x = 0; x < VoxelWorld._chunkSize; x++)
                        {
                            var vox = chunkBuffer.AtGet(x, y, z).Type;
                            if(vox != VoxelType.Empty)
                            {
                                var faces = voxelsVisibleFaces[x, y, z];
                                CreateCube(meshData, new Vector3(x, y, z) * VoxelWorld._blockSize, faces, new Vector3Int(x, y, z), vox);
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
                var normal = dir.ToVecInt();

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
                var centerInd = (rotationToDir * new Vector3(0, 0, 1)).ToInt();
                var leftInd = (rotationToDir * new Vector3(-1, 0, 1)).ToInt();
                var rightInd = (rotationToDir * new Vector3(1, 0, 1)).ToInt();
                var forwardInd = (rotationToDir * new Vector3(0, -1, 1)).ToInt();
                var backwardInd = (rotationToDir * new Vector3(0, 1, 1)).ToInt();
                var forwardLeftInd = (rotationToDir * new Vector3(-1, -1, 1)).ToInt();
                var forwardRightInd = (rotationToDir * new Vector3(1, -1, 1)).ToInt();
                var backwardLeftInd = (rotationToDir * new Vector3(-1, 1, 1)).ToInt();
                var backwardRightInd = (rotationToDir * new Vector3(1, 1, 1)).ToInt();

                centerInd += blockPos;
                leftInd += blockPos;
                rightInd += blockPos;
                forwardInd += blockPos;
                backwardInd += blockPos;
                forwardLeftInd += blockPos;
                forwardRightInd += blockPos;
                backwardLeftInd += blockPos;
                backwardRightInd += blockPos;

                var center = lightingData[centerInd.x, centerInd.y, centerInd.z];
                var left = lightingData[leftInd.x, leftInd.y, leftInd.z];
                var right = lightingData[rightInd.x, rightInd.y, rightInd.z];
                var front = lightingData[forwardInd.x, forwardInd.y, forwardInd.z];
                var back = lightingData[backwardInd.x, backwardInd.y, backwardInd.z];
                var frontLeft = lightingData[forwardLeftInd.x, forwardLeftInd.y, forwardLeftInd.z];
                var frontRight = lightingData[forwardRightInd.x, forwardRightInd.y, forwardRightInd.z];
                var backLeft = lightingData[backwardLeftInd.x, backwardLeftInd.y, backwardLeftInd.z];
                var backRight = lightingData[backwardRightInd.x, backwardRightInd.y, backwardRightInd.z];

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

            #endregion Mesh generation

            #region Copying
            private void CopyNeighboursLight(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                var voxLightBuff = chunksLight[chunk];
                for(int z = 1; z < VoxelWorld._chunkSize + 1; z++)
                    for(int y = 1; y < VoxelWorld._chunkSize + 1; y++)
                        for(int x = 1; x < VoxelWorld._chunkSize + 1; x++)
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
                const int sz = VoxelWorld._chunkSize;
                var neighb = neighbours.Up;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, z + 1] = nextVox.AtGet(x, 0, z);
                }

                neighb = neighbours.Down;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, z + 1] = nextVox.AtGet(x, sz - 1, z);
                }

                neighb = neighbours.Left;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, z + 1] = nextVox.AtGet(sz - 1, y, z);
                }

                neighb = neighbours.Right;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < sz; z++)
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, z + 1] = nextVox.AtGet(0, y, z);
                }

                neighb = neighbours.Backward;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int y = 0; y < sz; y++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, y + 1, 0] = nextVox.AtGet(x, y, sz - 1);
                }

                neighb = neighbours.Forward;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int y = 0; y < sz; y++)
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, y + 1, sz + 1] = nextVox.AtGet(x, y, 0);
                }
            }

            private void Copy12Edges(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxelWorld._chunkSize;
                if(neighbours.Up != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Up].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[sz + 1, sz + 1, z + 1] = nextVox.AtGet(0, 0, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Up].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[0, sz + 1, z + 1] = nextVox.AtGet(sz - 1, 0, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Up].Backward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, 0] = nextVox.AtGet(x, 0, sz - 1);
                    }

                    neighb = allChunksNeighbours[neighbours.Up].Forward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, sz + 1, sz + 1] = nextVox.AtGet(x, 0, 0);
                    }
                }
                if(neighbours.Down != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Down].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[sz + 1, 0, z + 1] = nextVox.AtGet(0, sz - 1, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Down].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < sz; z++)
                            copyTo[0, 0, z + 1] = nextVox.AtGet(sz - 1, sz - 1, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Down].Backward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, 0] = nextVox.AtGet(x, sz - 1, sz - 1);
                    }

                    neighb = allChunksNeighbours[neighbours.Down].Forward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < sz; x++)
                            copyTo[x + 1, 0, sz + 1] = nextVox.AtGet(x, sz - 1, 0);
                    }
                }
                if(neighbours.Forward != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Forward].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, sz + 1] = nextVox.AtGet(0, y, 0);
                    }

                    neighb = allChunksNeighbours[neighbours.Forward].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, sz + 1] = nextVox.AtGet(sz - 1, y, 0);
                    }
                }
                if(neighbours.Backward != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Backward].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[sz + 1, y + 1, 0] = nextVox.AtGet(0, y, sz - 1);
                    }

                    neighb = allChunksNeighbours[neighbours.Backward].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < sz; y++)
                            copyTo[0, y + 1, 0] = nextVox.AtGet(sz - 1, y, sz - 1);
                    }
                }
            }

            private void Copy8Vertices(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                const int sz = VoxelWorld._chunkSize;
                if(neighbours.Up != Entity.Null)
                {
                    var neighb1 = allChunksNeighbours[neighbours.Up];

                    if(neighb1.Left != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Left].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, sz + 1, sz + 1] = nextVox.AtGet(sz - 1, 0, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Left].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, sz + 1, 0] = nextVox.AtGet(sz - 1, 0, sz - 1);
                        }
                    }

                    if(neighb1.Right != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Right].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[sz + 1, sz + 1, sz + 1] = nextVox.AtGet(0, 0, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Right].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[sz + 1, sz + 1, 0] = nextVox.AtGet(0, 0, sz - 1);
                        }
                    }
                }
                if(neighbours.Down != Entity.Null)
                {
                    var neighb1 = allChunksNeighbours[neighbours.Down];

                    if(neighb1.Left != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Left].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, 0, sz + 1] = nextVox.AtGet(sz - 1, sz - 1, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Left].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, 0, 0] = nextVox.AtGet(sz - 1, sz - 1, sz - 1);
                        }
                    }

                    if(neighb1.Right != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Right].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[sz + 1, 0, sz + 1] = nextVox.AtGet(0, sz - 1, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Right].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[sz + 1, 0, 0] = nextVox.AtGet(0, sz - 1, sz - 1);
                        }
                    }
                }
            }

            #endregion Copying
        }

        [BurstCompile]
        public struct RebuildChunkBlockVisibleFacesJob : IJobParallelForBatch
        {
            [WriteOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            [ReadOnly]
            public Entity chunk;

            [ReadOnly]
            public ChunkNeighboursComponent neighbours;

            [ReadOnly]
            public BufferFromEntity<Voxel> chunks;

            public void Execute(int index, int count)
            {
                for(int k = 0; k < count; k++)
                {
                    facesVisibleArr.At(index + k, out int x, out int y, out int z);

                    var facesVisible = DirectionsHelper.BlockDirectionFlag.None;
                    for(byte i = 0; i < 6; i++)
                    {
                        var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                        var vec = dir.ToVecInt();
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
        }

        protected override void OnCreateManager()
        {
            _chunksDirty = EntityManager.CreateComponentGroup(
                typeof(RegularChunk),
                typeof(ChunkDirtyComponent),
                typeof(ChunkNeighboursComponent),
                typeof(Voxel),
                typeof(VoxelLightingLevel));
            RequireForUpdate(_chunksDirty);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var commandBuffer = _barrier.CreateCommandBuffer();
            var dirty = _chunksDirty.GetComponentArray<RegularChunk>();
            var entities = _chunksDirty.GetEntityArray();
            var neig = _chunksDirty.GetComponentDataArray<ChunkNeighboursComponent>();
            JobHandle handle;
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
                    VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                chunk = entity,
                chunks = GetBufferFromEntity<Voxel>(true),
                neighbours = neighb,
            };
            var hn1 = j1.ScheduleBatch(VoxelWorld._chunkSize * VoxelWorld._chunkSize * VoxelWorld._chunkSize, 1024, inputDeps);
            var hn2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                voxelsVisibleFaces = j1.facesVisibleArr,
                allChunksNeighbours = GetComponentDataFromEntity<ChunkNeighboursComponent>(true),
                chunk = entity,
                chunksLight = GetBufferFromEntity<VoxelLightingLevel>(true),
                chunksVox = GetBufferFromEntity<Voxel>(true),
                neighbours = neighb,
                lightingData = new NativeArray3D<VoxelLightingLevel>(VoxelWorld._chunkSize + 2, VoxelWorld._chunkSize + 2, VoxelWorld._chunkSize + 2, Allocator.TempJob),
            }.Schedule(hn1);

            return hn2;
        }

        #endregion Chunk processing

        #region Helper methods

        public static void ChunkVoxelCoordinates(Vector3 worldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            worldPos /= VoxelWorld._chunkSize;
            chunkPos = ((worldPos - (Vector3.one * (VoxelWorld._chunkSize / 2))) / VoxelWorld._chunkSize).ToInt();
            voxelPos = (worldPos - chunkPos * VoxelWorld._chunkSize).ToInt();
        }

        public static void ChunkVoxelCoordinates(Vector3Int voxelWorldPos, out Vector3Int chunkPos, out Vector3Int voxelPos)
        {
            chunkPos = ((voxelWorldPos - (Vector3.one * (VoxelWorld._chunkSize / 2))) / VoxelWorld._chunkSize).ToInt();
            voxelPos = (voxelWorldPos - chunkPos * VoxelWorld._chunkSize);
        }

        public static Vector3Int WorldPosToVoxelPos(Vector3 pos)
        {
            return (pos / VoxelWorld._chunkSize).ToInt();
        }

        public static Vector3 VoxelPosToWorldPos(Vector3Int pos)
        {
            return ((Vector3)pos * VoxelWorld._chunkSize);
        }

        #endregion Helper methods
    }
}