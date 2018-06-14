using Plugins.Helpers;
using Scripts.Help;
using Scripts.World.Jobs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public const int _chunkSize = 17;

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
                SetDirty(data._chunk);
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

            CreateStartingLevels(0, 2, 2);
        }

        public ChunkRebuildingVisibleFacesData RebuildChunkVisibleFaces(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var jb0 = new RebuildChunkBlockVisibleFacesJob()
            {
                facesVisibleArr = chunk.VoxelsVisibleFaces,

                boxThatContainsChunkAndAllNeighboursBorders = CopyGivenAndNeighbourBordersVoxels(chunk),
            };

            var hndl = jb0.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, dependency);
            JobHandle.ScheduleBatchedJobs();

            return new ChunkRebuildingVisibleFacesData()
            {
                _updateJob = hndl,
                _chunk = chunk,

                boxThatContainsChunkAndAllNeighboursBorders = jb0.boxThatContainsChunkAndAllNeighboursBorders,
            };
        }

        public ChunkCleaningData CleanChunk(RegularChunk chunk, JobHandle dependency = default(JobHandle))
        {
            var jb2 = new ConstructMeshJob()
            {
                meshData = chunk.MeshData,
                chunkAndNeighboursVoxels = CopyGivenAndNeighbourBordersVoxels(chunk),
                chunkAndNeighboursLighting = CopyGivenAndNeighbourBordersLighting(chunk),

                voxelsVisibleFaces = chunk.VoxelsVisibleFaces,
            };

            var hndl = jb2.Schedule(dependency);
            JobHandle.ScheduleBatchedJobs();

            return new ChunkCleaningData()
            {
                _chunk = chunk,
                _updateJob = hndl,

                boxThatContainsChunkAndAllNeighboursBordersLight = jb2.chunkAndNeighboursLighting,
                boxThatContainsChunkAndAllNeighboursBordersVox = jb2.chunkAndNeighboursVoxels,
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
                    var vec = dir.ToVecInt();

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

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightingLevels;

                var lightLvl = chunk.VoxelLightingLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];

                SetDirty(chunk);
                //check 6 sides
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

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

        private NativeArray3D<Voxel> CopyGivenAndNeighbourBordersVoxels(RegularChunk chunk)
        {
            var chunkVox = chunk.Voxels;

            var array = new NativeArray3D<Voxel>(_chunkSize + 2, _chunkSize + 2, _chunkSize + 2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            //fill array with air
            for (int i = 0; i < array.XMax * array.YMax * array.ZMax; i++)
            {
                array[i] = new Voxel()
                {
                    type = VoxelType.Air,
                };
            }

            //copy contents of chunk to this new array
            for (int z = 0; z < _chunkSize; z++)
            {
                for (int y = 0; y < _chunkSize; y++)
                {
                    for (int x = 0; x < _chunkSize; x++)
                    {
                        array[x + 1, y + 1, z + 1] = chunkVox[x, y, z];
                    }
                }
            }

            Check6Sides();

            Check12Edges();

            Check8Vertices();

            return array;

            void Check6Sides()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up;
                var vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, _chunkSize + 1, z + 1] = nextVox[x, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, 0, z + 1] = nextVox[x, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int y = 0; y < _chunkSize; y++)
                            array[0, y + 1, z + 1] = nextVox[_chunkSize - 1, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int y = 0; y < _chunkSize; y++)
                            array[_chunkSize + 1, y + 1, z + 1] = nextVox[0, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int y = 0; y < _chunkSize; y++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, 0] = nextVox[x, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int y = 0; y < _chunkSize; y++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, _chunkSize + 1] = nextVox[x, y, 0];
                }
            }
            void Check12Edges()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right;
                var vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, _chunkSize + 1, z + 1] = nextVox[0, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[0, _chunkSize + 1, z + 1] = nextVox[_chunkSize - 1, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, 0] = nextVox[x, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[x, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, 0, z + 1] = nextVox[0, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[0, 0, z + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, 0] = nextVox[x, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, _chunkSize + 1] = nextVox[x, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, _chunkSize + 1] = nextVox[0, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, 0] = nextVox[0, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, 0] = nextVox[_chunkSize - 1, y, _chunkSize - 1];
                }
            }
            void Check8Vertices()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                var vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, _chunkSize + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, _chunkSize + 1, 0] = nextVox[_chunkSize - 1, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[0, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, _chunkSize + 1, 0] = nextVox[0, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, 0, _chunkSize + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, 0, 0] = nextVox[_chunkSize - 1, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, 0, _chunkSize + 1] = nextVox[0, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, 0, 0] = nextVox[0, _chunkSize - 1, _chunkSize - 1];
                }
            }
        }

        private NativeArray3D<VoxelLightingLevel> CopyGivenAndNeighbourBordersLighting(RegularChunk chunk)
        {
            var chunkLight = chunk.VoxelLightingLevels;

            var array = new NativeArray3D<VoxelLightingLevel>(_chunkSize + 2, _chunkSize + 2, _chunkSize + 2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            //fill array with air
            for (int i = 0; i < array.XMax * array.YMax * array.ZMax; i++)
            {
                array[i] = new VoxelLightingLevel()
                {
                    _level = 0,
                };
            }

            //copy contents of chunk to this new array
            for (int z = 0; z < _chunkSize; z++)
            {
                for (int y = 0; y < _chunkSize; y++)
                {
                    for (int x = 0; x < _chunkSize; x++)
                    {
                        array[x + 1, y + 1, z + 1] = chunkLight[x, y, z];
                    }
                }
            }

            Check6Sides();

            Check12Edges();

            Check8Vertices();

            return array;

            void Check6Sides()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up;
                var vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, _chunkSize + 1, z + 1] = nextVox[x, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, 0, z + 1] = nextVox[x, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int y = 0; y < _chunkSize; y++)
                            array[0, y + 1, z + 1] = nextVox[_chunkSize - 1, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        for (int y = 0; y < _chunkSize; y++)
                            array[_chunkSize + 1, y + 1, z + 1] = nextVox[0, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int y = 0; y < _chunkSize; y++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, 0] = nextVox[x, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int y = 0; y < _chunkSize; y++)
                        for (int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, _chunkSize + 1] = nextVox[x, y, 0];
                }
            }
            void Check12Edges()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right;
                var vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, _chunkSize + 1, z + 1] = nextVox[0, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[0, _chunkSize + 1, z + 1] = nextVox[_chunkSize - 1, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, 0] = nextVox[x, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[x, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, 0, z + 1] = nextVox[0, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int z = 0; z < _chunkSize; z++)
                        array[0, 0, z + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, 0] = nextVox[x, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, _chunkSize + 1] = nextVox[x, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, _chunkSize + 1] = nextVox[0, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, 0] = nextVox[0, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    for (int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, 0] = nextVox[_chunkSize - 1, y, _chunkSize - 1];
                }
            }
            void Check8Vertices()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                var vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[0, _chunkSize + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[0, _chunkSize + 1, 0] = nextVox[_chunkSize - 1, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[_chunkSize + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[0, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[_chunkSize + 1, _chunkSize + 1, 0] = nextVox[0, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[0, 0, _chunkSize + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[0, 0, 0] = nextVox[_chunkSize - 1, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[_chunkSize + 1, 0, _chunkSize + 1] = nextVox[0, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if (IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightingLevels;
                    array[_chunkSize + 1, 0, 0] = nextVox[0, _chunkSize - 1, _chunkSize - 1];
                }
            }
        }

        public Voxel GetVoxel(Vector3Int chunkPos, Vector3Int blockPos)
        {
            return GetChunk(chunkPos).Voxels[blockPos.x, blockPos.y, blockPos.z];
        }

        private void SetDirty(RegularChunk ch)
        {
            if (ch.IsInitialized && !ch.IsBeingRebult)
            {
                ch.SetBeingRebuilt();
                _dirty.Enqueue(ch);
            }
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
            if (chunk.IsInitialized)
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

                if (ch.VoxelLightingLevels[blockPos.x, blockPos.y, blockPos.z]._level > 0)
                    //if this block is solid then remove light from this block
                    SetToRemoveLight(new VoxelLightPropagationData() { _blockPos = blockPos, _chunkPos = chunkPos });

                //check 6 sides of a voxel
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

                    DirectionsHelper.BlockDirectionFlag oppositeDir = dir.Opposite();

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
                                if (!nextVoxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                                    nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] |= oppositeDir;//enable side of the next block
                                else
                                    nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                                visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of this block

                                if (nextChunk.VoxelLightingLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level > 0)
                                    //if this block is air then propagate light here
                                    SetToPropagateLight(new VoxelLightPropagationData() { _blockPos = nextBlockPos, _chunkPos = nextChunkPos });
                            }
                            else
                            {
                                nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                                if (!nextVoxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())//if solid
                                    visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of the next block
                                else
                                    visibleSides[blockPos.x, blockPos.y, blockPos.z] |= dir;//enable side of the next block
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
                            if (!voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())//if solid
                                visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] |= oppositeDir;//enable side of the next block
                            else
                                visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                            visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of this block

                            if (ch.VoxelLightingLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z]._level > 0)
                                //if this block is air then propagate light here
                                SetToPropagateLight(new VoxelLightPropagationData() { _blockPos = nextBlockPos, _chunkPos = chunkPos });
                        }
                        else
                        {
                            visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                            if (!voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())//if solid
                                visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of the next block
                            else
                                visibleSides[blockPos.x, blockPos.y, blockPos.z] |= dir;//enable side of the next block
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
