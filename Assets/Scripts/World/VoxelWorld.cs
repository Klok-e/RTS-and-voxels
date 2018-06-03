using ProceduralNoiseProject;
using Scripts.Help;
using Scripts.World.Jobs;
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
        /// Only uneven amount or else SetVoxel won't work at all
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

        public ChunkUpdateData CleanChunk(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var jb0 = new RebuildChunkBlockVisibleFacesJob()
            {
                chunkPos = chunk.Pos,
                facesVisibleArr = chunk.VoxelsVisibleFaces,
                chunkSize = _chunkSize,

                voxels = new NativeArray3D<Voxel>(GetChunk(chunk.Pos).Voxels, Allocator.TempJob),
                voxelsFront = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Front).Voxels, Allocator.TempJob),
                voxelsBack = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Back).Voxels, Allocator.TempJob),
                voxelsUp = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Up).Voxels, Allocator.TempJob),
                voxelsDown = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Down).Voxels, Allocator.TempJob),
                voxelsLeft = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Left).Voxels, Allocator.TempJob),
                voxelsRight = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Right).Voxels, Allocator.TempJob),
            };
            var jb1 = new PropagateLightJob()
            {
                lightingLevels = chunk.VoxelLightingLevels,
                voxels = jb0.voxels,
            };
            var jb2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                voxels = jb0.voxels,
                voxelLightingLevels = jb1.lightingLevels,
                voxelsIsVisible = chunk.VoxelsIsVisible,
                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
            };

            var hndl = jb0.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, dependency);
            hndl = jb1.Schedule(hndl);
            hndl = jb2.Schedule(hndl);
            JobHandle.ScheduleBatchedJobs();

            return new ChunkUpdateData()
            {
                _chunk = chunk,
                _updateJob = hndl,
                _voxels = jb0.voxels,
                _voxelsFront = jb0.voxelsFront,
                _voxelsBack = jb0.voxelsBack,
                _voxelsUp = jb0.voxelsUp,
                _voxelsDown = jb0.voxelsDown,
                _voxelsLeft = jb0.voxelsLeft,
                _voxelsRight = jb0.voxelsRight,
                _lightingLevels = jb1.lightingLevels,
            };
        }

        public void CompleteChunkUpdate(ChunkUpdateData data)
        {
            data._updateJob.Complete();

            //data._lightingLevels.Dispose();
            data._voxels.Dispose();
            data._voxelsBack.Dispose();
            data._voxelsDown.Dispose();
            data._voxelsFront.Dispose();
            data._voxelsLeft.Dispose();
            data._voxelsRight.Dispose();
            data._voxelsUp.Dispose();
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

        public void SetLight(Vector3 blockWorldPos, byte level)
        {
            var chunkPos = ((blockWorldPos - (Vector3.one * (_chunkSize / 2))) / _chunkSize).ToInt();
            var blockPos = (blockWorldPos - chunkPos * _chunkSize).ToInt();

            var ch = GetChunk(chunkPos);
            if (ch.IsInitialized)
            {
                var t = ch.VoxelLightingLevels;
                t[blockPos.x, blockPos.y, blockPos.z] = new VoxelLightingLevel()
                {
                    Level = level,
                };
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
                        chunkSize = _chunkSize,
                        voxels = chunk.Voxels,
                    }.Schedule());
                    Dirty.Enqueue(chunk);
                    level[x, z] = chunk;
                }
            }

            _massJobThing.CompleteAll();
            return level;
        }

        #endregion Level generation
    }
}
