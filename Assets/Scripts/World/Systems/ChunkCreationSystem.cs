using Scripts.Help;
using Scripts.World.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class ChunkCreationSystem : ComponentSystem
    {
        private ComponentGroup _worldSpawners;
        private ComponentGroup _allChunks;
        private ComponentGroup _needToLoadNeighboursChunks;

        private Material _chunkMaterial;
        private Vector2Int _mapSize;
        private Dictionary<Vector3Int, Entity> _chunks;

        protected override void OnCreateManager()
        {
            _chunks = new Dictionary<Vector3Int, Entity>();
            _allChunks = EntityManager.CreateComponentGroup(typeof(RegularChunk));
            _worldSpawners = EntityManager.CreateComponentGroup(typeof(MapParameters));
            RequireForUpdate(_worldSpawners);
        }

        protected override void OnDestroyManager()
        {

        }

        protected override void OnUpdate()
        {
            using(var spawners = _worldSpawners.ToEntityArray(Allocator.TempJob))
            {
                if(spawners.Length > 0)
                {
                    var parameters = EntityManager.GetSharedComponentData<MapParameters>(spawners[0]);
                    PostUpdateCommands.DestroyEntity(spawners[0]);

                    _chunkMaterial = parameters._chunkMaterial;

                    SetTextureArray(parameters._textures);

                    for(int x = 0; x < parameters._size.x; x++)
                        for(int y = 0; y < parameters._size.y; y++)
                        {
                            CreateChunk(new Vector3Int(x, 0, y));
                        }
                }

            }
        }

        private void CreateChunk(Vector3Int pos)
        {
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(pos, _chunkMaterial);
            var ent = chunk.gameObject.AddComponent<GameObjectEntity>().Entity;
            EntityManager.AddComponent(ent, typeof(ChunkNeedTerrainGeneration));

            var buf1 = EntityManager.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxelWorld._chunkSize * VoxelWorld._chunkSize * VoxelWorld._chunkSize);
            //Debug.Log($"Length of voxel buffer: {buf1.Length}");

            var buf2 = EntityManager.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxelWorld._chunkSize * VoxelWorld._chunkSize * VoxelWorld._chunkSize);
            //Debug.Log($"Length of light buffer: {buf2.Length}");

            var neighbs = new ChunkNeighboursComponent();

            _chunks.Add(pos, ent);
            for(int i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                var dirVec = dir.ToVecInt();
                if(_chunks.ContainsKey(pos + dirVec))
                {
                    var nextEnt = _chunks[pos + dirVec];
                    var nextNeighb = EntityManager.GetComponentData<ChunkNeighboursComponent>(nextEnt);
                    nextNeighb[dir.Opposite()] = ent;
                    neighbs[dir] = nextEnt;
                    EntityManager.SetComponentData(nextEnt, nextNeighb);
                }
            }
            EntityManager.AddComponent(ent, typeof(ChunkNeighboursComponent));
            EntityManager.SetComponentData(ent, neighbs);
        }

        private void SetTextureArray(Texture2D[] textures)
        {
            var textureArray = new Texture2DArray(16, 16, textures.Length, TextureFormat.RGBA32, true);
            for(int i = 0; i < textures.Length; i++)
            {
                var pix = textures[i].GetPixels();
                textureArray.SetPixels(pix, i);
            }
            textureArray.Apply();

            textureArray.filterMode = FilterMode.Point;

            _chunkMaterial.SetTexture("_VoxelTextureArray", textureArray);
        }
    }
}
