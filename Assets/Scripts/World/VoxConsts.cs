using Unity.Mathematics;

namespace World
{
    public static class VoxConsts
    {
        /// <summary>
        ///     Only even amount or else SetVoxel won't work at all
        /// </summary>
        public const int ChunkSize = 16;

        /// <summary>
        ///     Size in chunks
        /// </summary>
        public const int RegionSize = 4;

        /// <summary>
        ///     Size of a voxel
        /// </summary>
        public const float BlockSize = 0.5f;

        public static int3 ChunkIn(float3 pos)
        {
            pos = OffsetWorld(pos);

            var worldPos       = FromWorldToVoxelWorldCoords(pos);
            var loaderChunkInf = worldPos / ChunkSize;
            var loaderChunkIn  = math.int3(math.floor(loaderChunkInf));
            return loaderChunkIn;
        }

        public static int3 RegionIn(float3 pos)
        {
            return math.int3(math.floor(math.float3(ChunkIn(pos)) / RegionSize));
        }

        public static int3 VoxIndexInChunk(float3 pos, int3 chunkPos)
        {
            pos = OffsetWorld(pos);

            pos = FromWorldToVoxelWorldCoords(pos);

            return math.int3(math.floor(pos - math.float3(chunkPos * ChunkSize)));
        }

        public static float3 FromWorldToVoxelWorldCoords(float3 pos)
        {
            return pos / BlockSize;
        }

        public static int3 VoxIndexInChunk(float3 pos)
        {
            return VoxIndexInChunk(pos, ChunkIn(pos));
        }

        private static float3 OffsetWorld(float3 pos)
        {
            pos.x += BlockSize / 2f;
            pos.y += BlockSize / 2f;
            pos.z += BlockSize / 2f;
            return pos;
        }

        /*
        #region Visible in inspector

        [SerializeField]
        private int _mapMaxX, _mapMaxZ;

        [SerializeField]
        private int _up, _down;

        [SerializeField]
        private Material _material;

        [SerializeField]
        private Texture2D[] _textures;

        #endregion Visible in inspector

        #region Private

        private Texture2DArray _textureArray;

        private Queue<ChunkCleaningData> _updateDataToProcess;
        private Queue<VoxelLightPropagationData> _toPropagateRegularLight;
        private Queue<VoxelLightPropagationData> _toRemoveRegularLight;
        private Queue<VoxelLightPropagationData> _toPropagateSunlight;
        private Queue<VoxelLightPropagationData> _toRemoveSunlight;
        private Queue<RegularChunk> _toRebuildVisibleFaces;
        private Queue<RegularChunk> _dirty;

        private Queue<VoxelChangeQueryData> _voxelsToChange;
        private Queue<LightChangeQueryData> _lightToChange;

        private ChunkContainer _chunks;

        private MassJobThing _massJobThing;

        #endregion Private

        #region MonoBehaviour implementations

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if(_toRebuildVisibleFaces.Count > 0)
            {
                var ch = _toRebuildVisibleFaces.Dequeue();
                var data = RebuildChunkVisibleFaces(ch);
                data.CompleteChunkVisibleFacesRebuilding();
                SetDirty(data._chunk);
            }

            EmptyQuerySetLightQueue();
            EmptyQuerySetVoxelQueue();

            DepropagateRegularLightSynchronously();
            DepropagateSunlightSynchronously();

            PropagateRegularLightSynchronously();
            PropagateSunlightSynchronously();

            if(_dirty.Count > 0)
            {
                int count = _dirty.Count > (Environment.ProcessorCount - 1) ? (Environment.ProcessorCount - 1) : _dirty.Count;
                for(int i = 0; i < count; i++)
                {
                    var ch = _dirty.Dequeue();
                    var data = CleanChunk(ch);
                    _updateDataToProcess.Enqueue(data);
                }
            }

            if(_updateDataToProcess.Count > 0)
            {
                int count = _updateDataToProcess.Count > (Environment.ProcessorCount - 1) ? (Environment.ProcessorCount - 1) : _updateDataToProcess.Count;
                for(int i = 0; i < count; i++)
                {
                    var data = _updateDataToProcess.Dequeue();
                    data.CompleteChunkCleaning();
                }
            }
        }

        public void OnDestroy()
        {
            foreach(var item in _chunks)
            {
                item.Deinitialize();
            }
        }

        #endregion MonoBehaviour implementations

        public void Initialize()
        {
            RegularChunk._chunkParent = transform;
            RegularChunk._material = _material;

            _chunks = new ChunkContainer(_mapMaxX, _mapMaxZ);
            _massJobThing = new MassJobThing();
            _dirty = new Queue<RegularChunk>();
            _updateDataToProcess = new Queue<ChunkCleaningData>();

            _voxelsToChange = new Queue<VoxelChangeQueryData>();
            _lightToChange = new Queue<LightChangeQueryData>();
            _toPropagateRegularLight = new Queue<VoxelLightPropagationData>();
            _toRemoveRegularLight = new Queue<VoxelLightPropagationData>();
            _toPropagateSunlight = new Queue<VoxelLightPropagationData>();
            _toRemoveSunlight = new Queue<VoxelLightPropagationData>();

            _toRebuildVisibleFaces = new Queue<RegularChunk>();

            CreateTextureArray();

            CreateStartingLevels(0, _up, _down);

            void CreateTextureArray()
            {
                _textureArray = new Texture2DArray(16, 16, _textures.Length, TextureFormat.RGBA32, true);
                for(int i = 0; i < _textures.Length; i++)
                {
                    var pix = _textures[i].GetPixels();
                    _textureArray.SetPixels(pix, i);
                }
                _textureArray.Apply();

                _textureArray.filterMode = FilterMode.Point;

                _material.SetTexture("_VoxelTextureArray", _textureArray);
            }
        }

        #region Chunk processing

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

        #endregion Chunk processing

        #region LightPropagation

        public void DepropagateRegularLightSynchronously()
        {
            while(_toRemoveRegularLight.Count > 0)
            {
                var data = _toRemoveRegularLight.Dequeue();
                var chunk = GetChunk(data._chunkPos);

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightLevels;

                var lightLvl = lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];
                lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z] = new VoxelLightingLevel(0, lightLvl.Sunlight);

                SetDirty(chunk);
                //check 6 sides
                for(int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

                    var nextBlockPos = data._blockPos + vec;

                    if(nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if(nextBlockPos.x >= _chunkSize)
                            nextBlockPos.x = 0;
                        else if(nextBlockPos.x < 0)
                            nextBlockPos.x = _chunkSize - 1;

                        if(nextBlockPos.y >= _chunkSize)
                            nextBlockPos.y = 0;
                        else if(nextBlockPos.y < 0)
                            nextBlockPos.y = _chunkSize - 1;

                        if(nextBlockPos.z >= _chunkSize)
                            nextBlockPos.z = 0;
                        else if(nextBlockPos.z < 0)
                            nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        if(IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var voxelsDir = nextChunk.Voxels;
                            var lightLvlDir = nextChunk.VoxelLightLevels;

                            if(voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                if(lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight < lightLvl.RegularLight)
                                {
                                    SetToRemoveRegularLight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                                else
                                {
                                    SetToPropagateRegularLight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                            }
                        }
                    }
                    else if(voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        if(lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight < lightLvl.RegularLight)
                        {
                            SetToRemoveRegularLight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                        else
                        {
                            SetToPropagateRegularLight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                    }
                }
            }
        }

        public void PropagateRegularLightSynchronously()
        {
            while(_toPropagateRegularLight.Count > 0)
            {
                var data = _toPropagateRegularLight.Dequeue();
                var chunk = GetChunk(data._chunkPos);

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightLevels;

                var lightLvl = chunk.VoxelLightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];

                SetDirty(chunk);
                //check 6 sides
                for(int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

                    var nextBlockPos = data._blockPos + vec;

                    if(nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if(nextBlockPos.x >= _chunkSize)
                            nextBlockPos.x = 0;
                        else if(nextBlockPos.x < 0)
                            nextBlockPos.x = _chunkSize - 1;

                        if(nextBlockPos.y >= _chunkSize)
                            nextBlockPos.y = 0;
                        else if(nextBlockPos.y < 0)
                            nextBlockPos.y = _chunkSize - 1;

                        if(nextBlockPos.z >= _chunkSize)
                            nextBlockPos.z = 0;
                        else if(nextBlockPos.z < 0)
                            nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        if(IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var voxelsDir = nextChunk.Voxels;
                            var lightLvlDir = nextChunk.VoxelLightLevels;

                            if(lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight < (lightLvl.RegularLight - 1)
                                &&
                                voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel(lightLvl.RegularLight - 1,
                                    lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight);
                                if(lightLvl.RegularLight - 1 > 0)
                                {
                                    SetToPropagateRegularLight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                            }
                        }
                    }
                    else if(lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight < (lightLvl.RegularLight - 1)
                             &&
                             voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel(lightLvl.RegularLight - 1,
                            lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight);
                        if(lightLvl.RegularLight - 1 > 0)
                        {
                            SetToPropagateRegularLight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                    }
                }
            }
        }

        public void DepropagateSunlightSynchronously()
        {
            while(_toRemoveSunlight.Count > 0)
            {
                var data = _toRemoveSunlight.Dequeue();
                var chunk = GetChunk(data._chunkPos);

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightLevels;

                var lightLvl = lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];
                lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z] = new VoxelLightingLevel(lightLvl.RegularLight, 0);

                SetDirty(chunk);
                //check 6 sides
                for(int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

                    var nextBlockPos = data._blockPos + vec;

                    if(nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if(nextBlockPos.x >= _chunkSize)
                            nextBlockPos.x = 0;
                        else if(nextBlockPos.x < 0)
                            nextBlockPos.x = _chunkSize - 1;

                        if(nextBlockPos.y >= _chunkSize)
                            nextBlockPos.y = 0;
                        else if(nextBlockPos.y < 0)
                            nextBlockPos.y = _chunkSize - 1;

                        if(nextBlockPos.z >= _chunkSize)
                            nextBlockPos.z = 0;
                        else if(nextBlockPos.z < 0)
                            nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        if(IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var voxelsDir = nextChunk.Voxels;
                            var lightLvlDir = nextChunk.VoxelLightLevels;

                            if(voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                if(dir == DirectionsHelper.BlockDirectionFlag.Down
                                    &&
                                    lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight == VoxelLightingLevel.maxLight)
                                {
                                    SetToRemoveSunlight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                                else if(lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight < lightLvl.Sunlight)
                                {
                                    SetToRemoveSunlight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                                else
                                {
                                    SetToPropagateSunlight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                            }
                        }
                    }
                    else if(voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        if(dir == DirectionsHelper.BlockDirectionFlag.Down
                            &&
                            lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight == VoxelLightingLevel.maxLight)
                        {
                            SetToRemoveSunlight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                        else if(lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight < lightLvl.Sunlight)
                        {
                            SetToRemoveSunlight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                        else
                        {
                            SetToPropagateSunlight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                    }
                }
            }
        }

        public void PropagateSunlightSynchronously()
        {
            while(_toPropagateSunlight.Count > 0)
            {
                var data = _toPropagateSunlight.Dequeue();
                var chunk = GetChunk(data._chunkPos);

                var voxels = chunk.Voxels;
                var lightLevels = chunk.VoxelLightLevels;

                var lightLvl = lightLevels[data._blockPos.x, data._blockPos.y, data._blockPos.z];

                SetDirty(chunk);
                //check 6 sides
                for(int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

                    var nextBlockPos = data._blockPos + vec;

                    if(nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if(nextBlockPos.x >= _chunkSize)
                            nextBlockPos.x = 0;
                        else if(nextBlockPos.x < 0)
                            nextBlockPos.x = _chunkSize - 1;

                        if(nextBlockPos.y >= _chunkSize)
                            nextBlockPos.y = 0;
                        else if(nextBlockPos.y < 0)
                            nextBlockPos.y = _chunkSize - 1;

                        if(nextBlockPos.z >= _chunkSize)
                            nextBlockPos.z = 0;
                        else if(nextBlockPos.z < 0)
                            nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = data._chunkPos + vec;
                        if(IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var voxelsDir = nextChunk.Voxels;
                            var lightLvlDir = nextChunk.VoxelLightLevels;

                            if(lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight < (lightLvl.Sunlight - 1)
                                &&
                                voxelsDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                            {
                                if(dir == DirectionsHelper.BlockDirectionFlag.Down && lightLvl.Sunlight == VoxelLightingLevel.maxLight)
                                {
                                    lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel(lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight,
                                        lightLvl.Sunlight);
                                }
                                else
                                {
                                    lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel(lightLvlDir[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight,
                                        lightLvl.Sunlight - 1);
                                }
                                if(lightLvl.Sunlight - 1 > 0)
                                {
                                    SetToPropagateSunlight(new VoxelLightPropagationData()
                                    {
                                        _blockPos = nextBlockPos,
                                        _chunkPos = nextChunkPos,
                                    });
                                }
                            }
                        }
                    }
                    else if(lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].Sunlight < (lightLvl.Sunlight - 1)
                             &&
                             voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                    {
                        if(dir == DirectionsHelper.BlockDirectionFlag.Down && lightLvl.Sunlight == VoxelLightingLevel.maxLight)
                        {
                            lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel(lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight,
                                lightLvl.Sunlight);
                        }
                        else
                        {
                            lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] = new VoxelLightingLevel(lightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].RegularLight,
                                lightLvl.Sunlight - 1);
                        }

                        if(lightLvl.Sunlight - 1 > 0)
                        {
                            SetToPropagateSunlight(new VoxelLightPropagationData()
                            {
                                _blockPos = nextBlockPos,
                                _chunkPos = data._chunkPos,
                            });
                        }
                    }
                }
            }
        }

        #endregion LightPropagation

        #region Copy voxel data

        private NativeArray3D<Voxel> CopyGivenAndNeighbourBordersVoxels(RegularChunk chunk)
        {
            var chunkVox = chunk.Voxels;

            var array = new NativeArray3D<Voxel>(_chunkSize + 2, _chunkSize + 2, _chunkSize + 2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            //fill array with air
            for(int i = 0; i < array.XMax * array.YMax * array.ZMax; i++)
            {
                array[i] = new Voxel()
                {
                    type = VoxelType.Air,
                };
            }

            //copy contents of chunk to this new array
            for(int z = 0; z < _chunkSize; z++)
            {
                for(int y = 0; y < _chunkSize; y++)
                {
                    for(int x = 0; x < _chunkSize; x++)
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
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, _chunkSize + 1, z + 1] = nextVox[x, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, 0, z + 1] = nextVox[x, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int y = 0; y < _chunkSize; y++)
                            array[0, y + 1, z + 1] = nextVox[_chunkSize - 1, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int y = 0; y < _chunkSize; y++)
                            array[_chunkSize + 1, y + 1, z + 1] = nextVox[0, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int y = 0; y < _chunkSize; y++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, 0] = nextVox[x, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int y = 0; y < _chunkSize; y++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, _chunkSize + 1] = nextVox[x, y, 0];
                }
            }
            void Check12Edges()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right;
                var vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, _chunkSize + 1, z + 1] = nextVox[0, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[0, _chunkSize + 1, z + 1] = nextVox[_chunkSize - 1, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, 0] = nextVox[x, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[x, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, 0, z + 1] = nextVox[0, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[0, 0, z + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, 0] = nextVox[x, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, _chunkSize + 1] = nextVox[x, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, _chunkSize + 1] = nextVox[0, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, 0] = nextVox[0, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, 0] = nextVox[_chunkSize - 1, y, _chunkSize - 1];
                }
            }
            void Check8Vertices()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                var vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, _chunkSize + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, _chunkSize + 1, 0] = nextVox[_chunkSize - 1, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[0, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, _chunkSize + 1, 0] = nextVox[0, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, 0, _chunkSize + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[0, 0, 0] = nextVox[_chunkSize - 1, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, 0, _chunkSize + 1] = nextVox[0, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).Voxels;
                    array[_chunkSize + 1, 0, 0] = nextVox[0, _chunkSize - 1, _chunkSize - 1];
                }
            }
        }

        private NativeArray3D<VoxelLightingLevel> CopyGivenAndNeighbourBordersLighting(RegularChunk chunk)
        {
            var chunkLight = chunk.VoxelLightLevels;

            var array = new NativeArray3D<VoxelLightingLevel>(_chunkSize + 2, _chunkSize + 2, _chunkSize + 2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            //fill array with air
            for(int i = 0; i < array.XMax * array.YMax * array.ZMax; i++)
            {
                array[i] = new VoxelLightingLevel()
                {
                    _level = 0,
                };
            }

            //copy contents of chunk to this new array
            for(int z = 0; z < _chunkSize; z++)
            {
                for(int y = 0; y < _chunkSize; y++)
                {
                    for(int x = 0; x < _chunkSize; x++)
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
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, _chunkSize + 1, z + 1] = nextVox[x, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, 0, z + 1] = nextVox[x, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int y = 0; y < _chunkSize; y++)
                            array[0, y + 1, z + 1] = nextVox[_chunkSize - 1, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        for(int y = 0; y < _chunkSize; y++)
                            array[_chunkSize + 1, y + 1, z + 1] = nextVox[0, y, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int y = 0; y < _chunkSize; y++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, 0] = nextVox[x, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int y = 0; y < _chunkSize; y++)
                        for(int x = 0; x < _chunkSize; x++)
                            array[x + 1, y + 1, _chunkSize + 1] = nextVox[x, y, 0];
                }
            }
            void Check12Edges()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right;
                var vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, _chunkSize + 1, z + 1] = nextVox[0, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[0, _chunkSize + 1, z + 1] = nextVox[_chunkSize - 1, 0, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, 0] = nextVox[x, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[x, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[_chunkSize + 1, 0, z + 1] = nextVox[0, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int z = 0; z < _chunkSize; z++)
                        array[0, 0, z + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, z];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, 0] = nextVox[x, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int x = 0; x < _chunkSize; x++)
                        array[x + 1, 0, _chunkSize + 1] = nextVox[x, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, _chunkSize + 1] = nextVox[0, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Front | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, y, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Right;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[_chunkSize + 1, y + 1, 0] = nextVox[0, y, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Back | DirectionsHelper.BlockDirectionFlag.Left;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    for(int y = 0; y < _chunkSize; y++)
                        array[0, y + 1, 0] = nextVox[_chunkSize - 1, y, _chunkSize - 1];
                }
            }
            void Check8Vertices()
            {
                var dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                var vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[0, _chunkSize + 1, _chunkSize + 1] = nextVox[_chunkSize - 1, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[0, _chunkSize + 1, 0] = nextVox[_chunkSize - 1, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[_chunkSize + 1, _chunkSize + 1, _chunkSize + 1] = nextVox[0, 0, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Up | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[_chunkSize + 1, _chunkSize + 1, 0] = nextVox[0, 0, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[0, 0, _chunkSize + 1] = nextVox[_chunkSize - 1, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Left | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[0, 0, 0] = nextVox[_chunkSize - 1, _chunkSize - 1, _chunkSize - 1];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Front;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[_chunkSize + 1, 0, _chunkSize + 1] = nextVox[0, _chunkSize - 1, 0];
                }

                dir = DirectionsHelper.BlockDirectionFlag.Down | DirectionsHelper.BlockDirectionFlag.Right | DirectionsHelper.BlockDirectionFlag.Back;
                vec = dir.ToVecInt();
                if(IsChunkPosInBordersOfTheMap(chunk.Pos + vec))
                {
                    var nextVox = GetChunk(chunk.Pos + vec).VoxelLightLevels;
                    array[_chunkSize + 1, 0, 0] = nextVox[0, _chunkSize - 1, _chunkSize - 1];
                }
            }
        }

        #endregion Copy voxel data

        #region Get something methods

        public Voxel GetVoxel(Vector3Int chunkPos, Vector3Int blockPos)
        {
            return GetChunk(chunkPos).Voxels[blockPos.x, blockPos.y, blockPos.z];
        }

        public Voxel GetVoxel(Vector3Int voxelWorldPos)
        {
            ChunkVoxelCoordinates(voxelWorldPos, out var chunkPos, out var voxelPos);
            return GetChunk(chunkPos).Voxels[voxelPos.x, voxelPos.y, voxelPos.z];
        }

        public Voxel GetVoxel(Vector3 worldPos)
        {
            ChunkVoxelCoordinates(worldPos, out var chunkPos, out var voxelPos);
            return GetChunk(chunkPos).Voxels[voxelPos.x, voxelPos.y, voxelPos.z];
        }

        public RegularChunk GetChunk(Vector3Int chunkPos)
        {
            if(!IsChunkPosInBordersOfTheMap(chunkPos))
            {
                var up = new Exception();
                throw up; //ha ha
            }
            var ch = _chunks[chunkPos.y][chunkPos.x, chunkPos.z];
            if(!ch.IsInitialized)
                throw new Exception();

            return ch;
        }

        #endregion Get something methods

        #region Add to queues methods

        private void SetDirty(RegularChunk ch)
        {
            if(ch.IsInitialized && !ch.IsBeingRebult)
            {
                ch.SetBeingRebuilt();
                _dirty.Enqueue(ch);
            }
        }

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

        private void SetToRebuildVisibleFaces(RegularChunk chunk)
        {
            if(chunk.IsInitialized)
            {
                _toRebuildVisibleFaces.Enqueue(chunk);
            }
        }

        #endregion Add to queues methods

        #region Resolve change voxel data queries

        private void EmptyQuerySetVoxelQueue()
        {
            while(_voxelsToChange.Count > 0)
            {
                var t = _voxelsToChange.Dequeue();
                SetVoxel(t.worldPos, t.newVoxelType);
            }
        }

        private void EmptyQuerySetLightQueue()
        {
            while(_lightToChange.Count > 0)
            {
                var t = _lightToChange.Dequeue();
                SetLight(t.worldPos, t.level);
            }
        }

        #endregion Resolve change voxel data queries

        #region Voxel editing

        private struct VoxelChangeQueryData
        {
            public Vector3 worldPos;
            public VoxelType newVoxelType;
        }

        private struct LightChangeQueryData
        {
            public Vector3 worldPos;
            public int level;
        }

        /// <summary>
        ///Set voxel at world coords (physics world coords)
        /// </summary>
        private void SetVoxel(Vector3 worldPos, VoxelType newVoxelType)
        {
            ChunkVoxelCoordinates(worldPos, out var chunkPos, out var blockPos);

            if(IsChunkPosInBordersOfTheMap(chunkPos))
            {
                var ch = GetChunk(chunkPos);

                var voxels = ch.Voxels;
                var visibleSides = ch.VoxelsVisibleFaces;

                voxels[blockPos.x, blockPos.y, blockPos.z] = new Voxel()
                {
                    type = newVoxelType,
                };

                if(ch.VoxelLightLevels[blockPos.x, blockPos.y, blockPos.z].IsAnyLightPresent)
                    //if this block is solid then remove light from this block
                    SetToRemoveAllLight(new VoxelLightPropagationData() { _blockPos = blockPos, _chunkPos = chunkPos });

                //check 6 sides of a voxel
                for(int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.ToVecInt();

                    DirectionsHelper.BlockDirectionFlag oppositeDir = dir.Opposite();

                    var nextBlockPos = blockPos + vec;

                    //if block corrds exceed this chunk
                    if(nextBlockPos.x >= _chunkSize || nextBlockPos.x < 0
                        ||
                        nextBlockPos.y >= _chunkSize || nextBlockPos.y < 0
                        ||
                        nextBlockPos.z >= _chunkSize || nextBlockPos.z < 0)
                    {
                        if(nextBlockPos.x >= _chunkSize)
                            nextBlockPos.x = 0;
                        else if(nextBlockPos.x < 0)
                            nextBlockPos.x = _chunkSize - 1;

                        if(nextBlockPos.y >= _chunkSize)
                            nextBlockPos.y = 0;
                        else if(nextBlockPos.y < 0)
                            nextBlockPos.y = _chunkSize - 1;

                        if(nextBlockPos.z >= _chunkSize)
                            nextBlockPos.z = 0;
                        else if(nextBlockPos.z < 0)
                            nextBlockPos.z = _chunkSize - 1;

                        var nextChunkPos = chunkPos + vec;
                        if(IsChunkPosInBordersOfTheMap(nextChunkPos))
                        {
                            var nextChunk = GetChunk(nextChunkPos);
                            SetDirty(nextChunk);

                            var nextVoxels = nextChunk.Voxels;
                            var nextVisibleSides = nextChunk.VoxelsVisibleFaces;

                            if(newVoxelType.IsAir())
                            {
                                if(!nextVoxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())
                                    nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] |= oppositeDir;//enable side of the next block
                                else
                                    nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                                visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of this block

                                if(nextChunk.VoxelLightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].IsAnyLightPresent)
                                    //if this block is air then propagate light here
                                    SetToPropagateAllLight(new VoxelLightPropagationData() { _blockPos = nextBlockPos, _chunkPos = nextChunkPos });
                            }
                            else
                            {
                                nextVisibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                                if(!nextVoxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())//if solid
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
                        if(newVoxelType.IsAir())
                        {
                            if(!voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())//if solid
                                visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] |= oppositeDir;//enable side of the next block
                            else
                                visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                            visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of this block

                            if(ch.VoxelLightLevels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].IsAnyLightPresent)
                                //if this block is air then propagate light here
                                SetToPropagateAllLight(new VoxelLightPropagationData() { _blockPos = nextBlockPos, _chunkPos = chunkPos });
                        }
                        else
                        {
                            visibleSides[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z] &= ~oppositeDir;//disable side of the next block

                            if(!voxels[nextBlockPos.x, nextBlockPos.y, nextBlockPos.z].type.IsAir())//if solid
                                visibleSides[blockPos.x, blockPos.y, blockPos.z] &= ~dir;//disable side of the next block
                            else
                                visibleSides[blockPos.x, blockPos.y, blockPos.z] |= dir;//enable side of the next block
                        }
                    }
                }

                SetDirty(ch);
            }
        }

        private void SetLight(Vector3 worldPos, int level)
        {
            ChunkVoxelCoordinates(worldPos, out var chunkPos, out var blockPos);

            if(IsChunkPosInBordersOfTheMap(chunkPos))
            {
                var ch = GetChunk(chunkPos);

                var t = ch.VoxelLightLevels;
                t[blockPos.x, blockPos.y, blockPos.z] = new VoxelLightingLevel(level, t[blockPos.x, blockPos.y, blockPos.z].Sunlight);
                SetToPropagateAllLight(new VoxelLightPropagationData()
                {
                    _blockPos = blockPos,
                    _chunkPos = chunkPos,
                });
            }
        }

        /// <summary>
        /// Insert a sphere in a world coordinate
        /// </summary>
        /// <param name="sphereWorldPos"></param>
        /// <param name="radiusInBlocks"></param>
        /// <param name="newVoxelType"></param>
        public void QueryInsertSphere(Vector3 sphereWorldPos, int radiusInBlocks, VoxelType newVoxelType)
        {
            for(float x = -radiusInBlocks; x < radiusInBlocks; x++)
            {
                for(float y = -radiusInBlocks; y < radiusInBlocks; y++)
                {
                    for(float z = -radiusInBlocks; z < radiusInBlocks; z++)
                    {
                        var pos = new Vector3(x, y, z);
                        if(pos.sqrMagnitude <= radiusInBlocks * radiusInBlocks)
                        {
                            QuerySetVoxel(sphereWorldPos + pos * _blockSize, newVoxelType);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///Query set voxel at world coords (physics world coords)
        /// </summary>
        public void QuerySetVoxel(Vector3 worldPos, VoxelType newVoxelType)
        {
            _voxelsToChange.Enqueue(new VoxelChangeQueryData()
            {
                newVoxelType = newVoxelType,
                worldPos = worldPos,
            });
        }

        public void QuerySetLight(Vector3 worldPos, int level)
        {
            _lightToChange.Enqueue(new LightChangeQueryData()
            {
                level = level,
                worldPos = worldPos,
            });
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
            while(true)
            {
                if(down > 0)
                {
                    down -= 1;
                    _chunks.AddLevel(false, GenerateTerrainLevel(false, false));
                }
                else if(up > 0)
                {
                    up -= 1;
                    _chunks.AddLevel(true, GenerateTerrainLevel(true, false));
                }
                else
                    break;
            }
            _massJobThing.CompleteAll();

            while(_toRebuildVisibleFaces.Count > 0)
            {
                var ch = _toRebuildVisibleFaces.Dequeue();
                var data = RebuildChunkVisibleFaces(ch);
                data.CompleteChunkVisibleFacesRebuilding();
                SetDirty(data._chunk);
            }
            while(_dirty.Count > 0)
            {
                var ch = _dirty.Dequeue();
                var data = CleanChunk(ch);
                _updateDataToProcess.Enqueue(data);
            }

            while(_updateDataToProcess.Count > 0)
            {
                var data = _updateDataToProcess.Dequeue();
                data.CompleteChunkCleaning();
            }
        }

        private RegularChunk[,] GenerateTerrainLevel(bool isUp, bool isFirstLevel)
        {
            int height = isUp ? _chunks.MaxHeight + 1 : _chunks.MinHeight - 1;
            if(isFirstLevel)
                height = _chunks.MinHeight;
            var level = new RegularChunk[_mapMaxX, _mapMaxZ];
            for(int z = 0; z < _mapMaxZ; z++)
            {
                for(int x = 0; x < _mapMaxX; x++)
                {
                    var chunk = RegularChunk.CreateNew();
                    chunk.Initialize(new Vector3Int(x, height, z));

                    _massJobThing.AddHandle(new GenerateChunkTerrainJob()
                    {
                        offset = new Vector3Int(x, height, z),
                        chunkSize = _chunkSize,
                        voxels = chunk.Voxels,
                        light = chunk.VoxelLightLevels,
                    }.Schedule());
                    SetToRebuildVisibleFaces(chunk);
                    level[x, z] = chunk;
                }
            }

            return level;
        }

        #endregion Level generation

        #region Helper methods

        public bool IsChunkPosInBordersOfTheMap(Vector3Int pos)
        {
            return pos.x < _mapMaxX && pos.z < _mapMaxZ && pos.x >= 0 && pos.z >= 0
                    &&
                    _chunks.ContainsHeight(pos.y);
        }

        public bool IsVoxelInBordersOfTheMap(Vector3Int pos)
        {
            ChunkVoxelCoordinates(pos, out var chunkPos, out var voxelPos);
            return IsChunkPosInBordersOfTheMap(chunkPos);
        }

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

        #endregion Helper methods*/
    }
}