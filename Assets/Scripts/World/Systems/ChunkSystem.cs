using Scripts.Help;
using Scripts.Help.DataContainers;
using Scripts.World.Components;
using System.Collections.Generic;
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

        //[BurstCompile]
        public struct ConstructMeshJob : IJob
        {
            [ReadOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;

            [ReadOnly]
            public Entity chunk;

            [ReadOnly]
            public ChunkNeighboursComponent neighbours;
            public ComponentDataFromEntity<ChunkNeighboursComponent> allChunksNeighbours;

            public BufferFromEntity<Voxel> chunksVox;
            public BufferFromEntity<VoxelLightingLevel> chunksLight;

            [WriteOnly]
            public NativeMeshData meshData;

            public void Execute()
            {
                var chunkBuffer = chunksVox[chunk];
                var lightingData = new NativeArray3D<VoxelLightingLevel>(VoxelWorld._chunkSize + 2, VoxelWorld._chunkSize + 2, VoxelWorld._chunkSize + 2,
                    Allocator.TempJob);
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
                lightingData.Dispose();

                #region Mesh generation

                void CreateCube(NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Vector3Int blockPos, VoxelType voxelType)
                {
                    for(int i = 0; i < 6; i++)
                    {
                        var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                        if((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                            CreateFace(mesh, pos, curr, blockPos, voxelType);
                    }
                }

                void CreateFace(NativeMeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Vector3Int blockPos, VoxelType voxelType)
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

                Vector4 CalculateLightForAFaceSmooth(Vector3Int blockPos, Vector3 normal, out bool isFlipped)
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

                float VertexLight(int center, int side1, int side2, int corner)
                {
                    return (center + side1 + side2 + corner) / 4f / 15f;
                }

                #endregion Mesh generation
            }

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
                var neighb = neighbours.Up;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < _chunkSize; z++)
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, _chunkSize + 1, z + 1] = nextVox.AtGet(x, 0, z);
                }

                neighb = neighbours.Down;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < _chunkSize; z++)
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, 0, z + 1] = nextVox.AtGet(x, _chunkSize - 1, z);
                }

                neighb = neighbours.Left;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < _chunkSize; z++)
                        for(int y = 0; y < _chunkSize; y++)
                            copyTo[0, y + 1, z + 1] = nextVox.AtGet(_chunkSize - 1, y, z);
                }

                neighb = neighbours.Right;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int z = 0; z < _chunkSize; z++)
                        for(int y = 0; y < _chunkSize; y++)
                            copyTo[_chunkSize + 1, y + 1, z + 1] = nextVox.AtGet(0, y, z);
                }

                neighb = neighbours.Backward;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int y = 0; y < _chunkSize; y++)
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, y + 1, 0] = nextVox.AtGet(x, y, _chunkSize - 1);
                }

                neighb = neighbours.Forward;
                if(neighb != Entity.Null)
                {
                    var nextVox = chunksLight[neighb];
                    for(int y = 0; y < _chunkSize; y++)
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, y + 1, _chunkSize + 1] = nextVox.AtGet(x, y, 0);
                }
            }

            private void Copy12Edges(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                if(neighbours.Up != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Up].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < _chunkSize; z++)
                            copyTo[_chunkSize + 1, _chunkSize + 1, z + 1] = nextVox.AtGet(0, 0, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Up].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < _chunkSize; z++)
                            copyTo[0, _chunkSize + 1, z + 1] = nextVox.AtGet(_chunkSize - 1, 0, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Up].Backward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, _chunkSize + 1, 0] = nextVox.AtGet(x, 0, _chunkSize - 1);
                    }

                    neighb = allChunksNeighbours[neighbours.Up].Forward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, _chunkSize + 1, _chunkSize + 1] = nextVox.AtGet(x, 0, 0);
                    }
                }
                if(neighbours.Down != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Down].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < _chunkSize; z++)
                            copyTo[_chunkSize + 1, 0, z + 1] = nextVox.AtGet(0, _chunkSize - 1, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Down].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int z = 0; z < _chunkSize; z++)
                            copyTo[0, 0, z + 1] = nextVox.AtGet(_chunkSize - 1, _chunkSize - 1, z);
                    }

                    neighb = allChunksNeighbours[neighbours.Down].Backward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, 0, 0] = nextVox.AtGet(x, _chunkSize - 1, _chunkSize - 1);
                    }

                    neighb = allChunksNeighbours[neighbours.Down].Forward;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int x = 0; x < _chunkSize; x++)
                            copyTo[x + 1, 0, _chunkSize + 1] = nextVox.AtGet(x, _chunkSize - 1, 0);
                    }
                }
                if(neighbours.Forward != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Forward].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < _chunkSize; y++)
                            copyTo[_chunkSize + 1, y + 1, _chunkSize + 1] = nextVox.AtGet(0, y, 0);
                    }

                    neighb = allChunksNeighbours[neighbours.Forward].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < _chunkSize; y++)
                            copyTo[0, y + 1, _chunkSize + 1] = nextVox.AtGet(_chunkSize - 1, y, 0);
                    }
                }
                if(neighbours.Backward != Entity.Null)
                {
                    var neighb = allChunksNeighbours[neighbours.Backward].Right;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < _chunkSize; y++)
                            copyTo[_chunkSize + 1, y + 1, 0] = nextVox.AtGet(0, y, _chunkSize - 1);
                    }

                    neighb = allChunksNeighbours[neighbours.Backward].Left;
                    if(neighb != Entity.Null)
                    {
                        var nextVox = chunksLight[neighb];
                        for(int y = 0; y < _chunkSize; y++)
                            copyTo[0, y + 1, 0] = nextVox.AtGet(_chunkSize - 1, y, _chunkSize - 1);
                    }
                }
            }

            private void Copy8Vertices(NativeArray3D<VoxelLightingLevel> copyTo)
            {
                if(neighbours.Up != Entity.Null)
                {
                    var neighb1 = allChunksNeighbours[neighbours.Up];

                    if(neighb1.Left != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Left].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, _chunkSize + 1, _chunkSize + 1] = nextVox.AtGet(_chunkSize - 1, 0, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Left].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, _chunkSize + 1, 0] = nextVox.AtGet(_chunkSize - 1, 0, _chunkSize - 1);
                        }
                    }

                    if(neighb1.Right != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Right].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[_chunkSize + 1, _chunkSize + 1, _chunkSize + 1] = nextVox.AtGet(0, 0, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Right].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[_chunkSize + 1, _chunkSize + 1, 0] = nextVox.AtGet(0, 0, _chunkSize - 1);
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
                            copyTo[0, 0, _chunkSize + 1] = nextVox.AtGet(_chunkSize - 1, _chunkSize - 1, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Left].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[0, 0, 0] = nextVox.AtGet(_chunkSize - 1, _chunkSize - 1, _chunkSize - 1);
                        }
                    }

                    if(neighb1.Right != Entity.Null)
                    {
                        var neighb2 = allChunksNeighbours[neighb1.Right].Forward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[_chunkSize + 1, 0, _chunkSize + 1] = nextVox.AtGet(0, _chunkSize - 1, 0);
                        }

                        neighb2 = allChunksNeighbours[neighb1.Right].Backward;
                        if(neighb2 != Entity.Null)
                        {
                            var nextVox = chunksLight[neighb2];
                            copyTo[_chunkSize + 1, 0, 0] = nextVox.AtGet(0, _chunkSize - 1, _chunkSize - 1);
                        }
                    }
                }
            }
        }

        //[BurstCompile]
        public struct RebuildChunkBlockVisibleFacesJob : IJobParallelFor
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

        protected override void OnCreateManager()
        {
            _chunksDirty = EntityManager.CreateComponentGroup(typeof(RegularChunk), typeof(ChunkDirtyComponent), typeof(ChunkNeighboursComponent), typeof(Voxel), typeof(VoxelLightingLevel));
            Initialize();
        }

        protected override void OnDestroyManager()
        {
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            inputDeps.Complete();

            var commandBuffer = _barrier.CreateCommandBuffer();

            var dirty = _chunksDirty.GetComponentArray<RegularChunk>();
            var entities = _chunksDirty.GetEntityArray();
            var neig = _chunksDirty.GetComponentDataArray<ChunkNeighboursComponent>();
            for(int i = 0; i < dirty.Length; i++)
            {
                CleanChunk(dirty[i], entities[i], neig[i]);
                commandBuffer.RemoveComponent<ChunkDirtyComponent>(entities[i]);
            }

            //EmptyQuerySetLightQueue();
            //EmptyQuerySetVoxelQueue();

            //DepropagateRegularLightSynchronously();
            //DepropagateSunlightSynchronously();

            //PropagateRegularLightSynchronously();
            //PropagateSunlightSynchronously();
            return default;
        }

        /// <summary>
        /// Only uneven amount or else SetVoxel won't work at all
        /// </summary>
        public const int _chunkSize = 17;

        /// <summary>
        /// Size of a voxel
        /// </summary>
        public const float _blockSize = 0.5f;

        #region Visible in inspector

        #endregion Visible in inspector

        #region Private

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
        }

        #region Chunk processing

        public void CleanChunk(RegularChunk chunk, Entity entity, ChunkNeighboursComponent neighb)
        {
            var hn1 = new RebuildChunkBlockVisibleFacesJob()
            {
                facesVisibleArr = chunk.VoxelsVisibleFaces,
                chunk = entity,
                chunks = GetBufferFromEntity<Voxel>(),
                neighbours = neighb,
            }.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024);

            var hn2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
                allChunksNeighbours = GetComponentDataFromEntity<ChunkNeighboursComponent>(),
                chunk = entity,
                chunksLight = GetBufferFromEntity<VoxelLightingLevel>(),
                chunksVox = GetBufferFromEntity<Voxel>(),
                neighbours = neighb,
            }.Schedule(hn1);

            hn2.Complete();

            chunk.ApplyMeshData();
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