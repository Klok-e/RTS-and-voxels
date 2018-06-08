using Plugins.Helpers;
using Scripts.Help;
using Scripts.World.Jobs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World
{
    public class VoxelWorldController : MonoBehaviour
    {
        [SerializeField] private int _mapMaxX, _mapMaxZ;

        [SerializeField] private Material _material;

        [SerializeField] private Color[] _colors;

        private Queue<ChunkUpdateData> _updateDataToProcess;
        private Queue<VoxelLightPropagationData> _toPropagateLight;

        /// <summary>
        /// Only uneven amount or else SetVoxel won't work at all
        /// </summary>
        public const int _chunkSize = 33;

        public const float _blockSize = 0.5f;

        public static VoxelWorldController Instance { get; private set; }

        private ChunkContainer _chunks;
        private Transform _chunkParent;
        private MassJobThing _massJobThing;

        public Queue<RegularChunk> Dirty { get; private set; }

        //public  Disposer _disposer;

        private void Awake()
        {
            VoxelExtensions.colors = _colors;
            RegularChunk._material = _material;
            RegularChunk._chunkParent = transform;

            UnityThread.InitUnityThread();
            Initialize();
            Instance = this;
        }

        private void Update()
        {
            if (Dirty.Count > 0)
            {
                //int count = VoxelWorld.Instance.Dirty.Count > (System.Environment.ProcessorCount - 1) ? (System.Environment.ProcessorCount - 1) : VoxelWorld.Instance.Dirty.Count;

                var ch = Dirty.Dequeue();
                var data = CleanChunk(ch);
                _updateDataToProcess.Enqueue(data);
            }

            if (_toPropagateLight.Count > 0)
            {
                PropagateAllLightSynchronously();
            }

            if (_updateDataToProcess.Count > 0)
            {
                var data = _updateDataToProcess.Dequeue();
                CompleteChunkUpdate(data);
            }
        }

        private void OnDestroy()
        {
            foreach (var item in _chunks)
            {
                item.Deinitialize();
            }
        }

        public void GenerateLevel(bool isUp)
        {
            _chunks.AddLevel(isUp, GenerateTerrainLevel(isUp, false));
        }

        public void Initialize()
        {
            _chunkParent = transform;
            _chunks = new ChunkContainer(_mapMaxX, _mapMaxZ);
            _massJobThing = new MassJobThing(0);
            Dirty = new Queue<RegularChunk>();
            _updateDataToProcess = new Queue<ChunkUpdateData>();
            _toPropagateLight = new Queue<VoxelLightPropagationData>();

            //_placeholderChunk = CreatePlaceholderChunk();

            CreateStartingLevels(0, 2, 1);
        }

        private RegularChunk CreatePlaceholderChunk()
        {
            var ch = RegularChunk.CreateNew();
            ch.Initialize(Vector3Int.zero);
            ch.gameObject.SetActive(false);

            for (int i = 0; i < _chunkSize * _chunkSize * _chunkSize; i++)
            {
                var v = ch.Voxels;
                v[i] = new Voxel()
                {
                    type = VoxelType.Air,
                };
            }
            return ch;
        }

        public ChunkUpdateData CleanChunk(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var adjacent = GetAdjacentChunkVoxels(chunk);

            var jb0 = new RebuildChunkBlockVisibleFacesJob()
            {
                chunkPos = chunk.Pos,
                facesVisibleArr = chunk.VoxelsVisibleFaces,
                chunkSize = _chunkSize,
                availableChunks = adjacent.dirChunksAvailable,

                voxels = new NativeArray3D<Voxel>(adjacent.chunk, Allocator.TempJob),
                voxelsFront = new NativeArray3D<Voxel>(adjacent.chunkFront, Allocator.TempJob),
                voxelsBack = new NativeArray3D<Voxel>(adjacent.chunkBack, Allocator.TempJob),
                voxelsUp = new NativeArray3D<Voxel>(adjacent.chunkUp, Allocator.TempJob),
                voxelsDown = new NativeArray3D<Voxel>(adjacent.chunkDown, Allocator.TempJob),
                voxelsLeft = new NativeArray3D<Voxel>(adjacent.chunkLeft, Allocator.TempJob),
                voxelsRight = new NativeArray3D<Voxel>(adjacent.chunkRight, Allocator.TempJob),
            };
            for (int i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);

                if ((dir & adjacent.dirChunksAvailable) == 0)
                {
                    switch (dir)
                    {
                        case DirectionsHelper.BlockDirectionFlag.Up:
                            adjacent.chunkUp.Dispose();
                            break;

                        case DirectionsHelper.BlockDirectionFlag.Down:
                            adjacent.chunkDown.Dispose();
                            break;

                        case DirectionsHelper.BlockDirectionFlag.Left:
                            adjacent.chunkLeft.Dispose();
                            break;

                        case DirectionsHelper.BlockDirectionFlag.Right:
                            adjacent.chunkRight.Dispose();
                            break;

                        case DirectionsHelper.BlockDirectionFlag.Back:
                            adjacent.chunkBack.Dispose();
                            break;

                        case DirectionsHelper.BlockDirectionFlag.Front:
                            adjacent.chunkFront.Dispose();
                            break;

                        default:
                            throw new Exception();
                    }
                }
            }
            var jb2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                voxels = jb0.voxels,
                voxelLightingLevels = chunk.VoxelLightingLevels,
                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
            };

            var hndl = jb0.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, dependency);
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
                //_lightingLevels = jb1.lightingLevels,
            };
        }

        public void CompleteChunkUpdate(ChunkUpdateData data)
        {
            data._updateJob.Complete();
            data._chunk.ApplyMeshData();

            data._voxels.Dispose();
            data._voxelsBack.Dispose();
            data._voxelsDown.Dispose();
            data._voxelsFront.Dispose();
            data._voxelsLeft.Dispose();
            data._voxelsRight.Dispose();
            data._voxelsUp.Dispose();
        }

        public PropagateLightJobData PropagateLight(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var jb = new PropagateLightJob()
            {
                chunkSize = _chunkSize,
                chunksAffected = new NativeArray<DirectionsHelper.BlockDirectionFlag>(1, Allocator.TempJob),

                lightingLevels = GetChunk(chunk.Pos).VoxelLightingLevels,
                lightingLevelsFront = GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Front).VoxelLightingLevels,
                lightingLevelsBack = GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Back).VoxelLightingLevels,
                lightingLevelsUp = GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Up).VoxelLightingLevels,
                lightingLevelsDown = GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Down).VoxelLightingLevels,
                lightingLevelsLeft = GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Left).VoxelLightingLevels,
                lightingLevelsRight = GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Right).VoxelLightingLevels,

                voxels = new NativeArray3D<Voxel>(GetChunk(chunk.Pos).Voxels, Allocator.TempJob),
                voxelsFront = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Front).Voxels, Allocator.TempJob),
                voxelsBack = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Back).Voxels, Allocator.TempJob),
                voxelsUp = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Up).Voxels, Allocator.TempJob),
                voxelsDown = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Down).Voxels, Allocator.TempJob),
                voxelsLeft = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Left).Voxels, Allocator.TempJob),
                voxelsRight = new NativeArray3D<Voxel>(GetChunk(chunk.Pos + DirectionsHelper.VectorDirections.Right).Voxels, Allocator.TempJob),
            };
            var hndl = jb.Schedule(dependency);
            JobHandle.ScheduleBatchedJobs();

            return new PropagateLightJobData()
            {
                voxels = jb.voxels,
                voxelsFront = jb.voxelsFront,
                voxelsBack = jb.voxelsBack,
                voxelsUp = jb.voxelsUp,
                voxelsDown = jb.voxelsDown,
                voxelsLeft = jb.voxelsLeft,
                voxelsRight = jb.voxelsRight,
                chunksAffected = jb.chunksAffected,
                jobHandle = hndl,
            };
        }

        public void PropagateAllLightSynchronously()
        {
            while (_toPropagateLight.Count > 0)
            {
                var data = _toPropagateLight.Dequeue();
                var chunk = GetChunk(data._chunkPos);
                SetDirty(chunk);

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightingLevels;

                var lightLvl = chunk.VoxelLightingLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];

                //check 6 sides of a voxel
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.DirectionToVec();

                    var blockPos = data._blockPos + vec;

                    if (blockPos.x >= _chunkSize || blockPos.x < 0
                        ||
                        blockPos.y >= _chunkSize || blockPos.y < 0
                        ||
                        blockPos.z >= _chunkSize || blockPos.z < 0)
                    {
                        if (blockPos.x >= _chunkSize) blockPos.x = 0;
                        else if (blockPos.x < 0) blockPos.x = _chunkSize - 1;

                        if (blockPos.y >= _chunkSize) blockPos.y = 0;
                        else if (blockPos.y < 0) blockPos.y = _chunkSize - 1;

                        if (blockPos.z >= _chunkSize) blockPos.z = 0;
                        else if (blockPos.z < 0) blockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        var nextChunk = GetChunk(nextChunkPos);
                        SetDirty(nextChunk);

                        var voxelsDir = nextChunk.Voxels;
                        var lightLvlDir = nextChunk.VoxelLightingLevels;

                        if (lightLvlDir[blockPos.x, blockPos.y, blockPos.z]._level < (lightLvl._level - 1))
                        {
                            lightLvlDir[blockPos.x, blockPos.y, blockPos.z] = new VoxelLightingLevel()
                            {
                                _level = (byte)(lightLvl._level - 1),
                            };
                            if (lightLvl._level - 1 > 0)
                            {
                                _toPropagateLight.Enqueue(new VoxelLightPropagationData()
                                {
                                    _blockPos = blockPos,
                                    _chunkPos = nextChunkPos,
                                });
                            }
                        }
                    }
                    else if (lightLevels[blockPos.x, blockPos.y, blockPos.z]._level < (lightLvl._level - 1))
                    {
                        lightLevels[blockPos.x, blockPos.y, blockPos.z] = new VoxelLightingLevel()
                        {
                            _level = (byte)(lightLvl._level - 1),
                        };
                        if (lightLvl._level - 1 > 0)
                        {
                            _toPropagateLight.Enqueue(new VoxelLightPropagationData()
                            {
                                _blockPos = blockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                    }
                }
            }
        }

        public RegularChunk GetChunk(Vector3Int chunkPos)
        {
            if (!IsPosInBordersOfTheMap(chunkPos))
            {
                var up = new Exception();
                throw up;
            }
            var ch = _chunks[chunkPos.y][chunkPos.x, chunkPos.z];
            if (!ch.IsInitialized)
                throw new Exception();

            return ch;
        }

        public struct ChunkAndAdjacent<T>
            where T : struct
        {
            public DirectionsHelper.BlockDirectionFlag dirChunksAvailable;

            public NativeArray3D<T> chunk;
            public NativeArray3D<T> chunkFront;
            public NativeArray3D<T> chunkBack;
            public NativeArray3D<T> chunkUp;
            public NativeArray3D<T> chunkDown;
            public NativeArray3D<T> chunkLeft;
            public NativeArray3D<T> chunkRight;
        }

        public ChunkAndAdjacent<Voxel> GetAdjacentChunkVoxels(RegularChunk chunk)
        {
            if (!chunk.IsInitialized || !IsPosInBordersOfTheMap(chunk.Pos))
                throw new Exception();

            var dirChunksAvailable = DirectionsHelper.BlockDirectionFlag.None;

            var front = chunk.Pos + DirectionsHelper.VectorDirections.Front;
            var back = chunk.Pos + DirectionsHelper.VectorDirections.Back;
            var up = chunk.Pos + DirectionsHelper.VectorDirections.Up;
            var down = chunk.Pos + DirectionsHelper.VectorDirections.Down;
            var left = chunk.Pos + DirectionsHelper.VectorDirections.Left;
            var right = chunk.Pos + DirectionsHelper.VectorDirections.Right;

            NativeArray3D<Voxel> chunkFront;
            NativeArray3D<Voxel> chunkBack;
            NativeArray3D<Voxel> chunkUp;
            NativeArray3D<Voxel> chunkDown;
            NativeArray3D<Voxel> chunkLeft;
            NativeArray3D<Voxel> chunkRight;
            if (IsPosInBordersOfTheMap(front))
            {
                chunkFront = GetChunk(front).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Front;
            }
            else chunkFront = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsPosInBordersOfTheMap(back))
            {
                chunkBack = GetChunk(back).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Back;
            }
            else chunkBack = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsPosInBordersOfTheMap(up))
            {
                chunkUp = GetChunk(up).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Up;
            }
            else chunkUp = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsPosInBordersOfTheMap(down))
            {
                chunkDown = GetChunk(down).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Down;
            }
            else chunkDown = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsPosInBordersOfTheMap(left))
            {
                chunkLeft = GetChunk(left).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Left;
            }
            else chunkLeft = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsPosInBordersOfTheMap(right))
            {
                chunkRight = GetChunk(right).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Right;
            }
            else chunkRight = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            return new ChunkAndAdjacent<Voxel>()
            {
                dirChunksAvailable = dirChunksAvailable,

                chunk = chunk.Voxels,
                chunkFront = chunkFront,
                chunkBack = chunkBack,
                chunkUp = chunkUp,
                chunkDown = chunkDown,
                chunkLeft = chunkLeft,
                chunkRight = chunkRight,
            };
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

        private void SetToPropagateLight(VoxelLightPropagationData data)
        {
            if (!_toPropagateLight.Contains(data))
            {
                _toPropagateLight.Enqueue(data);
            }
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

        public void SetLight(Vector3 blockWorldPos, byte level)
        {
            var chunkPos = ((blockWorldPos - (Vector3.one * (_chunkSize / 2))) / _chunkSize).ToInt();
            var blockPos = (blockWorldPos - chunkPos * _chunkSize).ToInt();

            var ch = GetChunk(chunkPos);

            var t = ch.VoxelLightingLevels;
            t[blockPos.x, blockPos.y, blockPos.z] = new VoxelLightingLevel()
            {
                _level = level,
            };
            SetToPropagateLight(new VoxelLightPropagationData()
            {
                _blockPos = blockPos,
                _chunkPos = chunkPos,
            });
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

        #region Helper methods

        private bool IsPosInBordersOfTheMap(Vector3Int pos)
        {
            return pos.x < _mapMaxX && pos.z < _mapMaxZ && pos.x >= 0 && pos.z >= 0
                    &&
                    _chunks.ContainsHeight(pos.y);
        }

        #endregion Helper methods
    }
}
