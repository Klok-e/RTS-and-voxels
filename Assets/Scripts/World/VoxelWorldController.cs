using Scripts.Help;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Plugins.Helpers;
using Unity.Jobs;
using ProceduralNoiseProject;
using Unity.Collections;

namespace Scripts.World
{
    public class VoxelWorldController : MonoBehaviour
    {
        [SerializeField] private int _mapLength, _mapWidth;

        [SerializeField] private float _blockSize;

        [SerializeField] private Material _material;

        [SerializeField] private Color32[] _colors;

        private void Awake()
        {
            VoxelExtensions.colors = _colors;
            Chunk._material = _material;

            UnityThread.InitUnityThread();
            VoxelWorld.Initialize(_mapLength, _mapWidth, transform);
        }

        private struct CoolJob : IJob
        {
            public Array3DNative<float> array3D;

            public void Execute()
            {
                array3D[5] = Mathf.Sin(5);
            }
        }

        private void Start()
        {
        }

        private void Update()
        {
        }

        private void OnApplicationQuit()
        {
        }
    }

    public static class VoxelWorld
    {
        public const int _chunkSize = 32;
        public const float _blockSize = 1f;

        private static ChunkContainer _chunks;
        private static Queue<Chunk> _dirtyChunks;
        private static int _mapMaxX;
        private static int _mapMaxZ;
        private static Transform _chunkParent;
        private static MassJobThing _massJobThing;

        //public static Disposer _disposer;

        private static Chunk _airChunk;
        private static Chunk _solidChunk;

        public static void Initialize(int mapMaxX, int maMaxZ, Transform chunkParent)
        {
            _chunkParent = chunkParent;
            _mapMaxX = mapMaxX;
            _mapMaxZ = maMaxZ;
            _chunks = new ChunkContainer(mapMaxX, maMaxZ);
            _massJobThing = new MassJobThing(1000);
            _dirtyChunks = new Queue<Chunk>();
            //_disposer = new Disposer(1000);

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            _airChunk = InitChunk(VoxelType.Air);
            _solidChunk = InitChunk(VoxelType.Solid);

            CreateStartingLevels(0, 5, 1);
            Debug.Log($"{watch.ElapsedMilliseconds} elapsed after generating levels");
            var handle = _massJobThing.CombineAll();
            Debug.Log($"{watch.ElapsedMilliseconds} elapsed after combining JobHandles");

            foreach (var item in _dirtyChunks)
            {
                var hndl = new RebuildChunkBlockVisibleFacesJob()
                {
                    chunkPos = item.Pos,
                    facesVisibleArr = item.VoxelsVisibleFaces,
                    voxels = GetChunk(item.Pos).Voxels,
                    voxelsBack = GetChunk(item.Pos + DirectionsHelper.BlockDirectionFlag.Back.DirectionToVec()).Voxels,
                    voxelsDown = GetChunk(item.Pos + DirectionsHelper.BlockDirectionFlag.Down.DirectionToVec()).Voxels,
                    voxelsFront = GetChunk(item.Pos + DirectionsHelper.BlockDirectionFlag.Front.DirectionToVec()).Voxels,
                    voxelsLeft = GetChunk(item.Pos + DirectionsHelper.BlockDirectionFlag.Left.DirectionToVec()).Voxels,
                    voxelsRight = GetChunk(item.Pos + DirectionsHelper.BlockDirectionFlag.Right.DirectionToVec()).Voxels,
                    voxelsUp = GetChunk(item.Pos + DirectionsHelper.BlockDirectionFlag.Up.DirectionToVec()).Voxels,
                }.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, handle);
                _massJobThing.AddHandle(hndl);
            }
            handle = _massJobThing.CombineAll();
            foreach (var item in _dirtyChunks)
            {
                var hndl = new RebuildVisibilityOfVoxelsJob()
                {
                    chunkPos = item.Pos,
                    facesVisibleArr = item.VoxelsVisibleFaces,
                    voxelsToRebuild = item.Voxels,
                }.Schedule(_chunkSize * _chunkSize * _chunkSize, 1024, handle);
                _massJobThing.AddHandle(hndl);
            }
            handle = _massJobThing.CombineAll();
            Debug.Log($"{watch.ElapsedMilliseconds} elapsed after scheduling the rebuilding of visible faces");

            handle.Complete();
            Debug.Log($"{watch.ElapsedMilliseconds} elapsed after Complete()");
            CleanAllDirty();
            Debug.Log($"{watch.ElapsedMilliseconds} elapsed after CleanAllDirty()");
        }

        private static Chunk InitChunk(VoxelType type)
        {
            var ch = CreateChunkObj();
            ch.gameObject.SetActive(false);

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

        public static void CleanAllDirty()
        {
            for (int i = 0; i < _dirtyChunks.Count; i++)
            {
                var ch = _dirtyChunks.Dequeue();
                ch.SetMeshData(ch.ConstructMesh());
            }
        }

        public static Chunk GetChunk(Vector3Int chunkPos)
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

        public static Voxel GetVoxel(Vector3Int chunkPos, Vector3Int blockPos)
        {
            return GetChunk(chunkPos).Voxels[blockPos.x, blockPos.y, blockPos.z];
        }

        public static void SetVoxel(Vector2Int chunkPos, int height, Vector3Int blockPos, VoxelType newVoxelType)
        {
            if (chunkPos.x < _mapMaxX && chunkPos.y < _mapMaxZ && chunkPos.x >= 0 && chunkPos.y >= 0)
            {
                Debug.LogError($"Wrong coordinate {chunkPos} not in range ({_mapMaxX}, {_mapMaxZ})");
                return;
            }
            var voxels = _chunks[height][chunkPos.x, chunkPos.y].Voxels;
            var vox = voxels[blockPos.x, blockPos.y, blockPos.z];
            voxels[blockPos.x, blockPos.y, blockPos.z] = new Voxel()
            {
                isVisible = vox.isVisible,
                type = newVoxelType,
            };
            //RebuildChunkBlocksAdjacentBlocks(new Vector3Int(chunkPos.x, height, chunkPos.y), voxels);
            //if(blockPos.x==0|| blockPos.y==0|| blockPos.z==0|| blockPos.x==_chunkSize-1|| blockPos)
        }

        #region Level generation

        private static void CreateStartingLevels(int startingHeight, int up, int down)
        {
            _chunks.InitializeStartingLevel(startingHeight, GenerateLevel(true, true));
            while (true)
            {
                if (down > 0)
                {
                    down -= 1;
                    _chunks.AddLevel(false, GenerateLevel(false, false));
                }
                else if (up > 0)
                {
                    up -= 1;
                    _chunks.AddLevel(true, GenerateLevel(true, false));
                }
                else
                    break;
            }
        }

        private static Chunk[,] GenerateLevel(bool isUp, bool isFirstLevel)
        {
            int height = isUp ? _chunks.MaxHeight + 1 : _chunks.MinHeight - 1;
            if (isFirstLevel)
                height = _chunks.MinHeight;
            var level = new Chunk[_mapMaxX, _mapMaxZ];
            for (int z = 0; z < _mapMaxZ; z++)
            {
                for (int x = 0; x < _mapMaxX; x++)
                {
                    var chunk = CreateChunkObj();
                    chunk.Initialize(new Vector3Int(x, height, z));

                    _massJobThing.AddHandle(new GenerateChunkTerrainJob()
                    {
                        offset = new Vector3Int(x, height, z),
                        voxels = chunk.Voxels
                    }.Schedule());

                    level[x, z] = chunk;

                    _dirtyChunks.Enqueue(chunk);
                }
            }
            return level;
        }

        #endregion Level generation

        private static bool DoesVoxelExceedBordersOfMapInDirection(Vector3Int chunkPos, Vector3Int voxelInd, DirectionsHelper.BlockDirectionFlag dirToLook)
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
                    adjacentChunkPos.x < _mapMaxX && adjacentChunkPos.z < _mapMaxZ) ? false : true;
            }
        }

        private static Chunk CreateChunkObj()
        {
            Chunk chunkObj;
            var go = new GameObject("Chunk");
            go.transform.parent = _chunkParent;

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            chunkObj = go.AddComponent<Chunk>();
            chunkObj.GetComponent<Renderer>().material = Chunk._material;
            return chunkObj;
        }

        #region Jobs

        private struct GenerateChunkTerrainJob : IJob
        {
            [ReadOnly]
            public Vector3Int offset;

            [WriteOnly]
            public Array3DNative<Voxel> voxels;

            public void Execute()
            {
                var fractal = new FractalNoise(new PerlinNoise(42, 3.0f), 2, 0.1f)
                {
                    Offset = offset
                };

                for (int x = 0; x < _chunkSize; x++)
                {
                    for (int z = 0; z < _chunkSize; z++)
                    {
                        for (int y = 0; y < _chunkSize; y++)
                        {
                            float fx = x / (_chunkSize - 1f);
                            float fz = z / (_chunkSize - 1f);
                            float fy = y / (_chunkSize - 1f);
                            var fill = fractal.Sample3D(fx, fy, fz);
                            //Debug.Log(fill + " " + $"({fx},{fz},{fy})" + " " + offset);
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
            [ReadOnly]
            public Vector3Int chunkPos;

            [WriteOnly]
            public Array3DNative<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            [ReadOnly]
            public Array3DNative<Voxel> voxels,
                voxelsUp, voxelsDown, voxelsLeft, voxelsRight, voxelsBack, voxelsFront;

            public void Execute(int currentIndex)
            {
                int x, y, z;
                facesVisibleArr.At(currentIndex, out x, out y, out z);

                DirectionsHelper.BlockDirectionFlag facesVisible = DirectionsHelper.BlockDirectionFlag.None;
                for (byte i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.DirectionToVec();
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

                        Array3DNative<Voxel> ch;
                        switch (dir)
                        {
                            case DirectionsHelper.BlockDirectionFlag.None: throw new Exception();

                            case DirectionsHelper.BlockDirectionFlag.Up: ch = voxelsUp; break;

                            case DirectionsHelper.BlockDirectionFlag.Down: ch = voxelsDown; break;

                            case DirectionsHelper.BlockDirectionFlag.Left: ch = voxelsLeft; break;

                            case DirectionsHelper.BlockDirectionFlag.Right: ch = voxelsRight; break;

                            case DirectionsHelper.BlockDirectionFlag.Back: ch = voxelsBack; break;

                            case DirectionsHelper.BlockDirectionFlag.Front: ch = voxelsFront; break;
                            default: throw new Exception();
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
            public Vector3Int chunkPos;

            [WriteOnly]
            public Array3DNative<Voxel> voxelsToRebuild;

            [ReadOnly]
            public Array3DNative<DirectionsHelper.BlockDirectionFlag> facesVisibleArr;

            public void Execute(int index)
            {
                var facesVisible = facesVisibleArr[index];
                int x, y, z;
                voxelsToRebuild.At(index, out x, out y, out z);
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
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Right))
                        ||
                        ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Left)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Left))
                        ||
                        ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Front)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Front))
                        ||
                        ((facesVisible & (DirectionsHelper.BlockDirectionFlag.Back)) != 0
                        &&
                        !DoesVoxelExceedBordersOfMapInDirection(chunkPos, voxelIndex, DirectionsHelper.BlockDirectionFlag.Back)))
                    {
                        isVisible = BlittableBool.True;
                    }
                }
                voxelsToRebuild[index] = new Voxel()
                {
                    isVisible = isVisible,
                    type = voxelsToRebuild[index].type,
                };
            }
        }

        #endregion Jobs
    }
}
