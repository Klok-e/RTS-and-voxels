using ProceduralNoiseProject;
using Scripts.Help;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World
{
    public class VoxelWorld
    {
        /// <summary>
        /// Only uneven amount or else SetVoxel won't work correctly (at all)
        /// </summary>
        public const int _chunkSize = 33;

        public const float _blockSize = 0.5f;

        public static VoxelWorld Instance { get { return _instance ?? (_instance = new VoxelWorld()); } }
        private static VoxelWorld _instance;

        private ChunkContainer _chunks;
        private int _mapMaxX;
        private int _mapMaxZ;
        private Transform _chunkParent;
        private MassJobThing _massJobThing;

        public Queue<RegularChunk> Dirty { get; private set; }

        //public  Disposer _disposer;

        private RegularChunk _airChunk;
        private RegularChunk _solidChunk;

        public void Initialize(int mapMaxX, int maMaxZ, Transform chunkParent)
        {
            _chunkParent = chunkParent;
            _mapMaxX = mapMaxX;
            _mapMaxZ = maMaxZ;
            _chunks = new ChunkContainer(mapMaxX, maMaxZ);
            _massJobThing = new MassJobThing(1000);
            Dirty = new Queue<RegularChunk>();

            _airChunk = InitChunk(VoxelType.Air);
            _solidChunk = InitChunk(VoxelType.Solid);

            CreateStartingLevels(0, 2, 1);
        }

        private RegularChunk InitChunk(VoxelType type)
        {
            var ch = RegularChunk.CreateNew();
            ch.Deinitialize();

            var vx = ch.Voxels;
            for (int i = 0; i < _chunkSize * _chunkSize * _chunkSize; i++)
            {
                vx[i] = new Voxel()
                {
                    type = type,
                };
            }
            return ch;
        }

        public JobHandle CleanChunk(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var hndl = new RebuildChunkBlockVisibleFacesJob()
            {
                chunkPos = chunk.Pos,
                facesVisibleArr = chunk.VoxelsVisibleFaces,
                voxels = GetChunk(chunk.Pos).Voxels,
                voxelsBack = GetChunk(chunk.Pos + DirectionsHelper.BlockDirectionFlag.Back.DirectionToVec()).Voxels,
                voxelsDown = GetChunk(chunk.Pos + DirectionsHelper.BlockDirectionFlag.Down.DirectionToVec()).Voxels,
                voxelsFront = GetChunk(chunk.Pos + DirectionsHelper.BlockDirectionFlag.Front.DirectionToVec()).Voxels,
                voxelsLeft = GetChunk(chunk.Pos + DirectionsHelper.BlockDirectionFlag.Left.DirectionToVec()).Voxels,
                voxelsRight = GetChunk(chunk.Pos + DirectionsHelper.BlockDirectionFlag.Right.DirectionToVec()).Voxels,
                voxelsUp = GetChunk(chunk.Pos + DirectionsHelper.BlockDirectionFlag.Up.DirectionToVec()).Voxels,
            }.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, dependency);
            hndl = new RebuildVisibilityOfVoxelsJob()
            {
                chunkPos = chunk.Pos,
                facesVisibleArr = chunk.VoxelsVisibleFaces,
                visibilityArrOfVoxelsToRebuild = chunk.VoxelsIsVisible,
                mapMaxX = _mapMaxX,
                mapMaxZ = _mapMaxZ,
            }.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, hndl);
            hndl = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                voxels = chunk.Voxels,
                voxelsIsVisible = chunk.VoxelsIsVisible,
                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
            }.Schedule(hndl);
            JobHandle.ScheduleBatchedJobs();
            return hndl;
        }

        public RegularChunk GetChunk(Vector3Int chunkPos)
        {
            if (chunkPos.x >= _mapMaxX || chunkPos.z >= _mapMaxZ || chunkPos.x < 0 || chunkPos.z < 0)
            {
                return _airChunk;
            }
            if (!_chunks.ContainsHeight(chunkPos.y))
            {
                return _solidChunk;
            }

            return _chunks[chunkPos.y][chunkPos.x, chunkPos.z];
        }

        public Voxel GetVoxel(Vector3Int chunkPos, Vector3Int blockPos)
        {
            return GetChunk(chunkPos).Voxels[blockPos.x, blockPos.y, blockPos.z];
        }

        private void SetDirty(RegularChunk ch)
        {
            if (ch.IsInitialized && !Dirty.Contains(ch))
                Dirty.Enqueue(ch);
        }

        #region Voxel editing

        /// <summary>
        ///Set voxel at block coords (posOfCollision/VoxelVorld._blockSize) (not physics world coords)
        /// </summary>
        /// <param name="blockWorldPos">In block coordinates</param>
        /// <param name="newVoxelType"></param>
        public void SetVoxel(Vector3 blockWorldPos, VoxelType newVoxelType)
        {
            var chunkPos = ((blockWorldPos - (Vector3.one * (_chunkSize / 2))) / _chunkSize).ToInt();
            var blockPos = (blockWorldPos - chunkPos * _chunkSize).ToInt();

            var ch = GetChunk(chunkPos);
            if (ch.IsInitialized)
            {
                var voxels = ch.Voxels;
                voxels[blockPos.x, blockPos.y, blockPos.z] = new Voxel()
                {
                    type = newVoxelType,
                };

                if (blockPos.y == (_chunkSize - 1))
                {
                    var v = GetChunk(chunkPos + DirectionsHelper.BlockDirectionFlag.Up.DirectionToVec());
                    SetDirty(v);
                }
                else if (blockPos.y == 0)
                {
                    var v = GetChunk(chunkPos + DirectionsHelper.BlockDirectionFlag.Down.DirectionToVec());
                    SetDirty(v);
                }

                if (blockPos.x == (_chunkSize - 1))
                {
                    var v = GetChunk(chunkPos + DirectionsHelper.BlockDirectionFlag.Right.DirectionToVec());
                    SetDirty(v);
                }
                else if (blockPos.x == 0)
                {
                    var v = GetChunk(chunkPos + DirectionsHelper.BlockDirectionFlag.Left.DirectionToVec());
                    SetDirty(v);
                }

                if (blockPos.z == (_chunkSize - 1))
                {
                    var v = GetChunk(chunkPos + DirectionsHelper.BlockDirectionFlag.Front.DirectionToVec());
                    SetDirty(v);
                }
                else if (blockPos.z == 0)
                {
                    var v = GetChunk(chunkPos + DirectionsHelper.BlockDirectionFlag.Back.DirectionToVec());
                    SetDirty(v);
                }

                SetDirty(ch);
            }
        }

        /// <summary>
        /// Insert a sphere in a block coordinate (posOfCollision/VoxelVorld._blockSize)
        /// </summary>
        /// <param name="sphereWorldPos"></param>
        /// <param name="radiusInBlocks"></param>
        /// <param name="newVoxelType"></param>
        public void InsertSphere(Vector3 sphereWorldPos, int radiusInBlocks, VoxelType newVoxelType)
        {
            for (int x = -radiusInBlocks; x < radiusInBlocks; x++)
            {
                for (int y = -radiusInBlocks; y < radiusInBlocks; y++)
                {
                    for (int z = -radiusInBlocks; z < radiusInBlocks; z++)
                    {
                        var pos = new Vector3(x, y, z);
                        if (pos.sqrMagnitude <= radiusInBlocks * radiusInBlocks)
                        {
                            SetVoxel(sphereWorldPos + pos, newVoxelType);
                        }
                    }
                }
            }
        }

        #endregion Voxel editing

        #region Level generation

        public void GenerateLevel(bool isUp)
        {
            _chunks.AddLevel(isUp, GenerateTerrainLevel(isUp, false));
        }

        private void CreateStartingLevels(int startingHeight, int up, int down)
        {
            _chunks.InitializeStartingLevel(startingHeight, GenerateTerrainLevel(true, true));
            while (true)
            {
                if (down > 0)
                {
                    down -= 1;
                    _chunks.AddLevel(false, GenerateTerrainLevel(false, false));
                }
                else if (up > 0)
                {
                    up -= 1;
                    _chunks.AddLevel(true, GenerateTerrainLevel(true, false));
                }
                else
                    break;
            }
        }

        private RegularChunk[,] GenerateTerrainLevel(bool isUp, bool isFirstLevel)
        {
            int height = isUp ? _chunks.MaxHeight + 1 : _chunks.MinHeight - 1;
            if (isFirstLevel)
                height = _chunks.MinHeight;
            var level = new RegularChunk[_mapMaxX, _mapMaxZ];
            for (int z = 0; z < _mapMaxZ; z++)
            {
                for (int x = 0; x < _mapMaxX; x++)
                {
                    var chunk = RegularChunk.CreateNew();
                    chunk.Initialize(new Vector3Int(x, height, z));

                    _massJobThing.AddHandle(new GenerateChunkTerrainJob()
                    {
                        offset = new Vector3Int(x, height, z),
                        voxels = chunk.Voxels
                    }.Schedule());
                    Dirty.Enqueue(chunk);
                    level[x, z] = chunk;
                }
            }
            _massJobThing.CompleteAll();
            return level;
        }

        #endregion Level generation

        private static bool DoesVoxelExceedBordersOfMapInDirection(Vector3Int chunkPos, Vector3Int voxelInd, DirectionsHelper.BlockDirectionFlag dirToLook, int mapMaxX, int mapMaxZ)
        {
            int x = voxelInd.x,
                y = voxelInd.y,
                z = voxelInd.z;

            var vec = dirToLook.DirectionToVec();
            if (x + vec.x < VoxelWorld._chunkSize && y + vec.y < VoxelWorld._chunkSize && z + vec.z < VoxelWorld._chunkSize
                &&
                x + vec.x >= 0 && y + vec.y >= 0 && z + vec.z >= 0)
            {
                return false;
            }
            else
            {
                var adjacentChunkPos = chunkPos + vec;
                return (adjacentChunkPos.x >= 0 && adjacentChunkPos.z >= 0
                    &&
                    adjacentChunkPos.x < mapMaxX && adjacentChunkPos.z < mapMaxZ) ? false : true;
            }
        }

        #region Jobs

        private struct GenerateChunkTerrainJob : IJob
        {
            [WriteOnly]
            public NativeArray3D<Voxel> voxels;

            [ReadOnly]
            public Vector3Int offset;

            public void Execute()
            {
                var fractal = new FractalNoise(new PerlinNoise(1337, 2.0f, 1.3f), 2, 0.2f, 1.5f)
                {
                    Offset = offset
                };

                for (int x = 0; x < _chunkSize; x++)
                {
                    for (int y = 0; y < _chunkSize; y++)
                    {
                        for (int z = 0; z < _chunkSize; z++)
                        {
                            float fx = x / (_chunkSize - 1f);
                            float fz = z / (_chunkSize - 1f);
                            float fy = y / (_chunkSize - 1f);
                            var fill = fractal.Sample3D(fx, fy, fz);

                            voxels[x, y, z] = new Voxel()
                            {
                                type = (fill * _chunkSize > y + (offset.y * _chunkSize)) ? VoxelType.Solid : VoxelType.Air,
                            };
                        }
                    }
                }
            }
        }

        private struct RebuildChunkBlockVisibleFacesJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            [ReadOnly]
            public Vector3Int chunkPos;

            [ReadOnly]
            public NativeArray3D<Voxel> voxels,
                voxelsUp, voxelsDown, voxelsLeft, voxelsRight, voxelsBack, voxelsFront;

            public void Execute(int currentIndex)
            {
                int x, y, z;
                facesVisibleArr.At(currentIndex, out x, out y, out z);

                DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
                for (byte i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    Vector3Int vec = dir.DirectionToVec();

                    if (x + vec.x < VoxelWorld._chunkSize && y + vec.y < VoxelWorld._chunkSize && z + vec.z < VoxelWorld._chunkSize
                        &&
                        x + vec.x >= 0 && y + vec.y >= 0 && z + vec.z >= 0)
                    {
                        if (voxels[x + vec.x, y + vec.y, z + vec.z].type.IsTransparent())
                            facesVisible |= dir;
                    }
                    else
                    {
                        var blockInd = (new Vector3Int(x, y, z) + vec);

                        if (blockInd.x >= VoxelWorld._chunkSize) blockInd.x = 0;
                        else if (blockInd.x < 0) blockInd.x = VoxelWorld._chunkSize - 1;

                        if (blockInd.y >= VoxelWorld._chunkSize) blockInd.y = 0;
                        else if (blockInd.y < 0) blockInd.y = VoxelWorld._chunkSize - 1;

                        if (blockInd.z >= VoxelWorld._chunkSize) blockInd.z = 0;
                        else if (blockInd.z < 0) blockInd.z = VoxelWorld._chunkSize - 1;

                        NativeArray3D<Voxel> ch;
                        switch (dir)
                        {
                            case DirectionsHelper.BlockDirectionFlag.None: ch = new NativeArray3D<Voxel>(); break;
                            case DirectionsHelper.BlockDirectionFlag.Up: ch = voxelsUp; break;
                            case DirectionsHelper.BlockDirectionFlag.Down: ch = voxelsDown; break;
                            case DirectionsHelper.BlockDirectionFlag.Left: ch = voxelsLeft; break;
                            case DirectionsHelper.BlockDirectionFlag.Right: ch = voxelsRight; break;
                            case DirectionsHelper.BlockDirectionFlag.Back: ch = voxelsBack; break;
                            case DirectionsHelper.BlockDirectionFlag.Front: ch = voxelsFront; break;
                            default: ch = new NativeArray3D<Voxel>(); break;
                        }

                        if ((ch[blockInd.x, blockInd.y, blockInd.z]).type.IsTransparent())
                            facesVisible |= dir;
                    }
                }
                facesVisibleArr[x, y, z] = facesVisible;
            }
        }

        private struct RebuildVisibilityOfVoxelsJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray3D<BlittableBool> visibilityArrOfVoxelsToRebuild;

            [ReadOnly]
            public Vector3Int chunkPos;

            [ReadOnly]
            public int mapMaxX, mapMaxZ;

            [ReadOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            public void Execute(int index)
            {
                var facesVisible = facesVisibleArr[index];
                int x, y, z;
                visibilityArrOfVoxelsToRebuild.At(index, out x, out y, out z);
                Vector3Int voxelIndex = new Vector3Int(x, y, z);

                BlittableBool isVisible = BlittableBool.False;
                if ((facesVisible & DirectionsHelper.BlockDirectionFlag.Up) != 0
                    ||
                    (facesVisible & DirectionsHelper.BlockDirectionFlag.Down) != 0)
                {
                    isVisible = BlittableBool.True;
                }
                else if ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Right)) != 0
                    ||
                    (facesVisible & (DirectionsHelper.BlockDirectionFlag.Left)) != 0
                    ||
                    (facesVisible & (DirectionsHelper.BlockDirectionFlag.Front)) != 0
                    ||
                    (facesVisible & (DirectionsHelper.BlockDirectionFlag.Back)) != 0)//if any of these flags is set
                {
                    //if any of the voxel's visible faces faces voxel that is not out of borders then block must be visible
                    if (((facesVisible & (DirectionsHelper.BlockDirectionFlag.Right)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Right, mapMaxX, mapMaxZ))
                        ||
                        ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Left)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Left, mapMaxX, mapMaxZ))
                        ||
                        ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Front)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Front, mapMaxX, mapMaxZ))
                        ||
                        ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Back)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Back, mapMaxX, mapMaxZ)))
                    {
                        isVisible = BlittableBool.True;
                    }
                }
                visibilityArrOfVoxelsToRebuild[index] = isVisible;
            }
        }

        private struct ConstructMeshJob : IJob
        {
            [ReadOnly]
            public NativeArray3D<Voxel> voxels;

            [ReadOnly]
            public NativeArray3D<BlittableBool> voxelsIsVisible;

            [ReadOnly]
            public NativeArray3D<DirectionsHelper.BlockDirectionFlag> voxelsVisibleFaces;

            [WriteOnly]
            public NativeMeshData meshData;

            public void Execute()
            {
                for (int x = 0; x < VoxelWorld._chunkSize; x++)
                {
                    for (int y = 0; y < VoxelWorld._chunkSize; y++)
                    {
                        for (int z = 0; z < VoxelWorld._chunkSize; z++)
                        {
                            if (voxels[x, y, z].type != VoxelType.Air)
                            {
                                var col = voxels[x, y, z].ToColor(voxelsIsVisible[x, y, z]);
                                CreateCube(ref meshData, new Vector3(x, y, z) * VoxelWorld._blockSize, voxelsVisibleFaces[x, y, z], col);
                            }
                        }
                    }
                }
            }

            #region Mesh generation

            private static void CreateCube(ref NativeMeshData mesh, Vector3 pos, DirectionsHelper.BlockDirectionFlag facesVisible, Color32 color)
            {
                for (int i = 0; i < 6; i++)
                {
                    var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    if ((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                        CreateFace(ref mesh, pos, curr, color);
                }
            }

            private static void CreateFace(ref NativeMeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Color32 color)
            {
                var startIndex = mesh._vertices.Length;

                Quaternion rotation = Quaternion.identity;

                switch (dir)
                {
                    case DirectionsHelper.BlockDirectionFlag.Left: rotation = Quaternion.LookRotation(Vector3.left); break;
                    case DirectionsHelper.BlockDirectionFlag.Right: rotation = Quaternion.LookRotation(Vector3.right); break;
                    case DirectionsHelper.BlockDirectionFlag.Down: rotation = Quaternion.LookRotation(Vector3.down); break;
                    case DirectionsHelper.BlockDirectionFlag.Up: rotation = Quaternion.LookRotation(Vector3.up); break;
                    case DirectionsHelper.BlockDirectionFlag.Back: rotation = Quaternion.LookRotation(Vector3.back); break;
                    case DirectionsHelper.BlockDirectionFlag.Front: rotation = Quaternion.LookRotation(Vector3.forward); break;
                    default: throw new Exception();
                }

                mesh._colors.Add(color);
                mesh._colors.Add(color);
                mesh._colors.Add(color);
                mesh._colors.Add(color);

                mesh._vertices.Add((rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorld._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorld._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorld._blockSize)) + vertOffset);
                mesh._vertices.Add((rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorld._blockSize)) + vertOffset);

                Vector3Int normal = dir.DirectionToVec();

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

            #endregion Mesh generation
        }

        #endregion Jobs
    }
}
