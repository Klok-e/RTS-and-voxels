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
        private static int _mapMaxY;
        private static Transform _chunkParent;

        public static void Initialize(int mapMaxX, int maMaxY, Transform chunkParent)
        {
            _chunkParent = chunkParent;
            _mapMaxX = mapMaxX;
            _mapMaxY = maMaxY;
            _chunks = new ChunkContainer(mapMaxX, maMaxY);
            _dirtyChunks = new Queue<Chunk>();

            for (int i = -1; i <= 3; i++)
            {
                GenerateLevel(i);
            }
            foreach (var item in _dirtyChunks)
            {
                RebuildChunkBlocksAdjacentBlocks(item.Pos, item.Voxels);
            }
            CleanAllDirty();
        }

        public static void CleanAllDirty()
        {
            for (int i = 0; i < _dirtyChunks.Count; i++)
            {
                var ch = _dirtyChunks.Dequeue();
                ch.SetMeshData(ch.ConstructMesh());
            }
        }

        public static Voxel GetVoxel(Vector2Int chunkPos, int height, Vector3Int blockPos)
        {
            if (chunkPos.x >= _mapMaxX || chunkPos.y >= _mapMaxY || chunkPos.x < 0 || chunkPos.y < 0)
            {
                return new Voxel()
                {
                    type = VoxelType.Air,
                };
            }
            if (!_chunks.ContainsHeight(height))
            {
                return new Voxel()
                {
                    type = VoxelType.Air,
                };
            }
            var lvl = _chunks[height];
            return lvl[chunkPos.x, chunkPos.y].Voxels[blockPos.x, blockPos.y, blockPos.z];
        }

        private static void GenerateLevel(int height)
        {
            var level = new Chunk[_mapMaxX, _mapMaxY];
            for (int y = 0; y < _mapMaxY; y++)
            {
                for (int x = 0; x < _mapMaxX; x++)
                {
                    var chunk = CreateChunkObj();
                    chunk.Initialize(new Vector3Int(x, height, y));
                    GenerateChunkTerrain(new Vector3Int(x, height, y), chunk.Voxels);
                    level[x, y] = chunk;

                    _dirtyChunks.Enqueue(chunk);
                }
            }

            _chunks[height] = level;
        }

        private static void RebuildChunkBlocksAdjacentBlocks(Vector3Int chunkPos, Array3DNative<Voxel> voxels)
        {
            for (int x = 0; x < _chunkSize; x++)
            {
                for (int z = 0; z < _chunkSize; z++)
                {
                    for (int y = 0; y < _chunkSize; y++)
                    {
                        DirectionsHelper.BlockDirectionFlag adjacentBlocks = DirectionsHelper.BlockDirectionFlag.None;
                        for (byte i = 0; i < 6; i++)
                        {
                            var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                            var vec = dir.DirectionToVec();
                            if (x + vec.x < VoxelWorld._chunkSize && y + vec.y < VoxelWorld._chunkSize && z + vec.z < VoxelWorld._chunkSize
                                &&
                                x + vec.x >= 0 && y + vec.y >= 0 && z + vec.z >= 0)
                            {
                                if (voxels[x + vec.x, y + vec.y, z + vec.z].type.IsTransparent())
                                    adjacentBlocks |= dir;
                            }
                            else
                            {
                                var blockInd = (new Vector3Int(x, y, z) + vec);
                                if (blockInd.x >= VoxelWorld._chunkSize) blockInd.x = 0;
                                if (blockInd.y >= VoxelWorld._chunkSize) blockInd.y = 0;
                                if (blockInd.z >= VoxelWorld._chunkSize) blockInd.z = 0;

                                if (blockInd.x < 0) blockInd.x = VoxelWorld._chunkSize - 1;
                                if (blockInd.y < 0) blockInd.y = VoxelWorld._chunkSize - 1;
                                if (blockInd.z < 0) blockInd.z = VoxelWorld._chunkSize - 1;

                                var adjacentChunkPos = chunkPos + vec;
                                if (VoxelWorld.GetVoxel(new Vector2Int(adjacentChunkPos.x, adjacentChunkPos.z), adjacentChunkPos.y, blockInd).type.IsTransparent())
                                    adjacentBlocks |= dir;
                            }
                        }

                        voxels[x, y, z] = new Voxel()
                        {
                            type = voxels[x, y, z].type,
                            adjacentBlocks = adjacentBlocks,
                        };
                    }
                }
            }
        }

        private static void GenerateChunkTerrain(Vector3Int offset, Array3DNative<Voxel> voxels)
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

                        voxels[x, y, z] = new Voxel()
                        {
                            type = (fill * _chunkSize > y + (offset.y * _chunkSize)) ? VoxelType.Solid : VoxelType.Air,
                            isVisible = BlittableBool.False,
                        };
                    }
                }
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
    }
}
