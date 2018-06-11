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

        private Queue<ChunkCleaningData> _updateDataToProcess;
        private Queue<VoxelLightPropagationData> _toPropagateLight;
        private Queue<VoxelLightPropagationData> _toRemoveLight;
        private Queue<RegularChunk> _toRebuildVisibleFaces;
        private Queue<RegularChunk> _dirty;

        /// <summary>
        /// Only uneven amount or else SetVoxel won't work at all
        /// </summary>
        public const int _chunkSize = 33;

        public const float _blockSize = 0.5f;

        public static VoxelWorldController Instance { get; private set; }

        private ChunkContainer _chunks;
        private Transform _chunkParent;
        private MassJobThing _massJobThing;

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
            if (_toRebuildVisibleFaces.Count > 0)
            {
                var ch = _toRebuildVisibleFaces.Dequeue();
                var data = RebuildChunkVisibleFaces(ch);
                data.CompleteChunkVisibleFacesRebuilding();
                _dirty.Enqueue(data._chunk);
            }

            if (_toPropagateLight.Count > 0)
            {
                PropagateAllLightSynchronously();
            }
            if (_toRemoveLight.Count > 0)
            {
                DepropagateAllLightSynchronously();
            }
            if (_toPropagateLight.Count > 0)
            {
                PropagateAllLightSynchronously();
            }

            if (_dirty.Count > 0)
            {
                int count = _dirty.Count > (Environment.ProcessorCount - 1) ? (Environment.ProcessorCount - 1) : _dirty.Count;
                for (int i = 0; i < count; i++)
                {
                    var ch = _dirty.Dequeue();
                    var data = CleanChunk(ch);
                    _updateDataToProcess.Enqueue(data);
                }
            }

            if (_updateDataToProcess.Count > 0)
            {
                int count = _updateDataToProcess.Count > (Environment.ProcessorCount - 1) ? (Environment.ProcessorCount - 1) : _updateDataToProcess.Count;
                for (int i = 0; i < count; i++)
                {
                    var data = _updateDataToProcess.Dequeue();
                    data.CompleteChunkCleaning();
                }
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
            _dirty = new Queue<RegularChunk>();
            _updateDataToProcess = new Queue<ChunkCleaningData>();
            _toPropagateLight = new Queue<VoxelLightPropagationData>();
            _toRemoveLight = new Queue<VoxelLightPropagationData>();
            _toRebuildVisibleFaces = new Queue<RegularChunk>();

            //_placeholderChunk = CreatePlaceholderChunk();

            CreateStartingLevels(0, 2, 1);
        }

        public ChunkRebuildingVisibleFacesData RebuildChunkVisibleFaces(RegularChunk chunk, JobHandle dependency = default(JobHandle))
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
            adjacent.DisposeUnavailable();

            var hndl = jb0.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, dependency);
            JobHandle.ScheduleBatchedJobs();

            return new ChunkRebuildingVisibleFacesData()
            {
                _updateJob = hndl,
                _chunk = chunk,

                _voxels = jb0.voxels,
                _voxelsBack = jb0.voxelsBack,
                _voxelsDown = jb0.voxelsDown,
                _voxelsFront = jb0.voxelsFront,
                _voxelsLeft = jb0.voxelsLeft,
                _voxelsRight = jb0.voxelsRight,
                _voxelsUp = jb0.voxelsUp,
            };
        }

        public ChunkCleaningData CleanChunk(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var adjacent = GetAdjacentChunkLightingLevels(chunk);

            var jb2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                voxels = new NativeArray3D<Voxel>(chunk.Voxels, Allocator.TempJob),

                availableChunks = adjacent.dirChunksAvailable,
                lightingLevels = new NativeArray3D<VoxelLightingLevel>(chunk.VoxelLightingLevels, Allocator.TempJob),
                lightingLevelsBack = new NativeArray3D<VoxelLightingLevel>(adjacent.chunkBack, Allocator.TempJob),
                lightingLevelsDown = new NativeArray3D<VoxelLightingLevel>(adjacent.chunkDown, Allocator.TempJob),
                lightingLevelsFront = new NativeArray3D<VoxelLightingLevel>(adjacent.chunkFront, Allocator.TempJob),
                lightingLevelsLeft = new NativeArray3D<VoxelLightingLevel>(adjacent.chunkLeft, Allocator.TempJob),
                lightingLevelsRight = new NativeArray3D<VoxelLightingLevel>(adjacent.chunkRight, Allocator.TempJob),
                lightingLevelsUp = new NativeArray3D<VoxelLightingLevel>(adjacent.chunkUp, Allocator.TempJob),
                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
            };
            adjacent.DisposeUnavailable();

            var hndl = jb2.Schedule(dependency);
            JobHandle.ScheduleBatchedJobs();

            return new ChunkCleaningData()
            {
                _chunk = chunk,
                _updateJob = hndl,
                _voxels = jb2.voxels,

                _lightingLevels = jb2.lightingLevels,
                _lightingLevelsBack = jb2.lightingLevelsBack,
                _lightingLevelsFront = jb2.lightingLevelsFront,
                _lightingLevelsUp = jb2.lightingLevelsUp,
                _lightingLevelsDown = jb2.lightingLevelsDown,
                _lightingLevelsLeft = jb2.lightingLevelsLeft,
                _lightingLevelsRight = jb2.lightingLevelsRight,
            };
        }

        public void DepropagateAllLightSynchronously()
        {
            while (_toRemoveLight.Count > 0)
            {
                var data = _toRemoveLight.Dequeue();
                var chunk = GetChunk(data._chunkPos);
                SetDirty(chunk);

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightingLevels;

                var lightLvl = lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];
                lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z] = new VoxelLightingLevel()
                {
                    _level = 0,
                };

                //check 6 sides
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.DirectionToVec();

                    var nextBlockPos = data._blockPos + vec;

                    if (nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if (nextBlockPos.x >= _chunkSize) nextBlockPos.x = 0;
                        else if (nextBlockPos.x < 0) nextBlockPos.x = _chunkSize - 1;

                        if (nextBlockPos.y >= _chunkSize) nextBlockPos.y = 0;
                        else if (nextBlockPos.y < 0) nextBlockPos.y = _chunkSize - 1;

                        if (nextBlockPos.z >= _chunkSize) nextBlockPos.z = 0;
                        else if (nextBlockPos.z < 0) nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        if (IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var voxelsDir = nextChunk.Voxels;
                            var lightLvlDir = nextChunk.VoxelLightingLevels;

                            if (lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level < lightLvl._level
                                &&
                                voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                SetToRemoveLight(new VoxelLightPropagationData()
                                {
                                    _blockPos = nextBlockPos,
                                    _chunkPos = nextChunkPos,
                                });
                            }
                            else if (voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                SetToPropagateLight(new VoxelLightPropagationData()
                                {
                                    _blockPos = nextBlockPos,
                                    _chunkPos = nextChunkPos,
                                });
                            }
                        }
                    }
                    else if (lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level < lightLvl._level
                             &&
                             voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        SetToRemoveLight(new VoxelLightPropagationData()
                        {
                            _blockPos = nextBlockPos,
                            _chunkPos = data._chunkPos,
                        });
                    }
                    else if (voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        SetToPropagateLight(new VoxelLightPropagationData()
                        {
                            _blockPos = nextBlockPos,
                            _chunkPos = data._chunkPos,
                        });
                    }
                }
            }
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

                //check 6 sides
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.DirectionToVec();

                    var nextBlockPos = data._blockPos + vec;

                    if (nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if (nextBlockPos.x >= _chunkSize) nextBlockPos.x = 0;
                        else if (nextBlockPos.x < 0) nextBlockPos.x = _chunkSize - 1;

                        if (nextBlockPos.y >= _chunkSize) nextBlockPos.y = 0;
                        else if (nextBlockPos.y < 0) nextBlockPos.y = _chunkSize - 1;

                        if (nextBlockPos.z >= _chunkSize) nextBlockPos.z = 0;
                        else if (nextBlockPos.z < 0) nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        if (IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var voxelsDir = nextChunk.Voxels;
                            var lightLvlDir = nextChunk.VoxelLightingLevels;

                            if (lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level < (lightLvl._level - 1)
                                &&
                                voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel()
                                {
                                    _level = (byte)(lightLvl._level - 1),
                                };
                                if (lightLvl._level - 1 > 0)
                                {
                                    _toPropagateLight.Enqueue(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                            }
                        }
                    }
                    else if (lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level < (lightLvl._level - 1)
                             &&
                             voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel()
                        {
                            _level = (byte)(lightLvl._level - 1),
                        };
                        if (lightLvl._level - 1 > 0)
                        {
                            _toPropagateLight.Enqueue(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                    }
                }
            }
        }

        public RegularChunk GetChunk(Vector3Int chunkPos)
        {
            if (!IsChunkPosInBordersOfTheMap(chunkPos))
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

            public void DisposeUnavailable()
            {
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    if ((dir & dirChunksAvailable) == 0)
                    {
                        switch (dir)
                        {
                            case DirectionsHelper.BlockDirectionFlag.Up: chunkUp.Dispose(); break;
                            case DirectionsHelper.BlockDirectionFlag.Down: chunkDown.Dispose(); break;
                            case DirectionsHelper.BlockDirectionFlag.Left: chunkLeft.Dispose(); break;
                            case DirectionsHelper.BlockDirectionFlag.Right: chunkRight.Dispose(); break;
                            case DirectionsHelper.BlockDirectionFlag.Back: chunkBack.Dispose(); break;
                            case DirectionsHelper.BlockDirectionFlag.Front: chunkFront.Dispose(); break;
                            default: throw new Exception();
                        }
                    }
                }
            }
        }

        public ChunkAndAdjacent<Voxel> GetAdjacentChunkVoxels(RegularChunk chunk)
        {
            if (!chunk.IsInitialized || !IsChunkPosInBordersOfTheMap(chunk.Pos))
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
            if (IsChunkPosInBordersOfTheMap(front))
            {
                chunkFront = GetChunk(front).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Front;
            }
            else chunkFront = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(back))
            {
                chunkBack = GetChunk(back).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Back;
            }
            else chunkBack = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(up))
            {
                chunkUp = GetChunk(up).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Up;
            }
            else chunkUp = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(down))
            {
                chunkDown = GetChunk(down).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Down;
            }
            else chunkDown = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(left))
            {
                chunkLeft = GetChunk(left).Voxels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Left;
            }
            else chunkLeft = new NativeArray3D<Voxel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(right))
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

        public ChunkAndAdjacent<VoxelLightingLevel> GetAdjacentChunkLightingLevels(RegularChunk chunk)
        {
            if (!chunk.IsInitialized || !IsChunkPosInBordersOfTheMap(chunk.Pos))
                throw new Exception();

            var dirChunksAvailable = DirectionsHelper.BlockDirectionFlag.None;

            var front = chunk.Pos + DirectionsHelper.VectorDirections.Front;
            var back = chunk.Pos + DirectionsHelper.VectorDirections.Back;
            var up = chunk.Pos + DirectionsHelper.VectorDirections.Up;
            var down = chunk.Pos + DirectionsHelper.VectorDirections.Down;
            var left = chunk.Pos + DirectionsHelper.VectorDirections.Left;
            var right = chunk.Pos + DirectionsHelper.VectorDirections.Right;

            NativeArray3D<VoxelLightingLevel> chunkFront;
            NativeArray3D<VoxelLightingLevel> chunkBack;
            NativeArray3D<VoxelLightingLevel> chunkUp;
            NativeArray3D<VoxelLightingLevel> chunkDown;
            NativeArray3D<VoxelLightingLevel> chunkLeft;
            NativeArray3D<VoxelLightingLevel> chunkRight;
            if (IsChunkPosInBordersOfTheMap(front))
            {
                chunkFront = GetChunk(front).VoxelLightingLevels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Front;
            }
            else chunkFront = new NativeArray3D<VoxelLightingLevel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(back))
            {
                chunkBack = GetChunk(back).VoxelLightingLevels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Back;
            }
            else chunkBack = new NativeArray3D<VoxelLightingLevel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(up))
            {
                chunkUp = GetChunk(up).VoxelLightingLevels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Up;
            }
            else chunkUp = new NativeArray3D<VoxelLightingLevel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(down))
            {
                chunkDown = GetChunk(down).VoxelLightingLevels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Down;
            }
            else chunkDown = new NativeArray3D<VoxelLightingLevel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(left))
            {
                chunkLeft = GetChunk(left).VoxelLightingLevels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Left;
            }
            else chunkLeft = new NativeArray3D<VoxelLightingLevel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            if (IsChunkPosInBordersOfTheMap(right))
            {
                chunkRight = GetChunk(right).VoxelLightingLevels;
                dirChunksAvailable |= DirectionsHelper.BlockDirectionFlag.Right;
            }
            else chunkRight = new NativeArray3D<VoxelLightingLevel>(0, 0, 0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            return new ChunkAndAdjacent<VoxelLightingLevel>()
            {
                dirChunksAvailable = dirChunksAvailable,

                chunk = chunk.VoxelLightingLevels,
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
            if (ch.IsInitialized && !_dirty.Contains(ch))
                _dirty.Enqueue(ch);
        }

        private void SetToPropagateLight(VoxelLightPropagationData data)
        {
            _toPropagateLight.Enqueue(data);
        }

        private void SetToRemoveLight(VoxelLightPropagationData data)
        {
            _toRemoveLight.Enqueue(data);
        }

        private void SetToRebuildVisibleFaces(RegularChunk chunk)
        {
            if (!_toRebuildVisibleFaces.Contains(chunk))
            {
                _toRebuildVisibleFaces.Enqueue(chunk);
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

            if (IsChunkPosInBordersOfTheMap(chunkPos))
            {
                var ch = GetChunk(chunkPos);

                var voxels = ch.Voxels;
                var visibleSides = ch.VoxelsVisibleFaces;

                voxels[blockPos.x, blockPos.y, blockPos.z] = new Voxel()
                {
                    type = newVoxelType,
                };

                //if this block is solid then remove light from this block
                SetToRemoveLight(new VoxelLightPropagationData() { _blockPos = blockPos, _chunkPos = chunkPos });

                //check 6 sides of a voxel
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.DirectionToVec();

                    DirectionsHelper.BlockDirectionFlag oppositeDir;
                    if (dir == DirectionsHelper.BlockDirectionFlag.Up || dir == DirectionsHelper.BlockDirectionFlag.Left || dir == DirectionsHelper.BlockDirectionFlag.Back)
                        oppositeDir = (DirectionsHelper.BlockDirectionFlag)(((byte)dir) << 1);
                    else
                        oppositeDir = (DirectionsHelper.BlockDirectionFlag)(((byte)dir) >> 1);

                    var nextBlockPos = blockPos + vec;

                    //if block corrds exceed this chunk
                    if (nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if (nextBlockPos.x >= _chunkSize) nextBlockPos.x = 0;
                        else if (nextBlockPos.x < 0) nextBlockPos.x = _chunkSize - 1;

                        if (nextBlockPos.y >= _chunkSize) nextBlockPos.y = 0;
                        else if (nextBlockPos.y < 0) nextBlockPos.y = _chunkSize - 1;

                        if (nextBlockPos.z >= _chunkSize) nextBlockPos.z = 0;
                        else if (nextBlockPos.z < 0) nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = chunkPos + vec;
                        if (IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var nextVoxels = nextChunk.Voxels;
                            var nextVisibleSides = nextChunk.VoxelsVisibleFaces;

                            if (newVoxelType.IsAir())
                            {
                                nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] |= oppositeDir;//enable side of the next block

                                visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of this block

                                //if this block is air then propagate light here
                                SetToPropagateLight(new VoxelLightPropagationData() { _blockPos = nextBlockPos, _chunkPos = nextChunkPos });
                            }
                            else
                            {
                                nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                                visibleSides[blockPos.x, blockPos.y, blockPos.z] |= dir;//enable side of this block
                            }
                        }
                        else
                        {
                            //if next chunk not in borders
                            //enable side of this block
                            visibleSides[blockPos.x, blockPos.y, blockPos.z] |= dir;
                        }
                    }
                    //if block coords are in borders of this chunk
                    else
                    {
                        if (newVoxelType.IsAir())
                        {
                            visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] |= oppositeDir;//enable side of the next block

                            visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of this block

                            //if this block is air then propagate light here
                            SetToPropagateLight(new VoxelLightPropagationData() { _blockPos = nextBlockPos, _chunkPos = chunkPos });
                        }
                        else
                        {
                            visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                            visibleSides[blockPos.x, blockPos.y, blockPos.z] |= dir;//enable side of this block
                        }
                    }
                }

                SetDirty(ch);
            }
        }

        public void SetLight(Vector3 blockWorldPos, byte level)
        {
            var chunkPos = ((blockWorldPos - (Vector3.one * (_chunkSize / 2))) / _chunkSize).ToInt();
            var blockPos = (blockWorldPos - chunkPos * _chunkSize).ToInt();

            if (IsChunkPosInBordersOfTheMap(chunkPos))
            {
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
                    SetToRebuildVisibleFaces(chunk);
                    level[x, z] = chunk;
                }
            }

            _massJobThing.CompleteAll();
            return level;
        }

        #endregion Level generation

        #region Helper methods

        private bool IsChunkPosInBordersOfTheMap(Vector3Int pos)
        {
            return pos.x < _mapMaxX && pos.z < _mapMaxZ && pos.x >= 0 && pos.z >= 0
                    &&
                    _chunks.ContainsHeight(pos.y);
        }

        #endregion Helper methods
    }
}
