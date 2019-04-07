using Scripts.Help;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class ChunkCreationSystem : ComponentSystem
    {
        private ComponentGroup _worldSpawners;
        //private ComponentGroup _allChunks;
        //private ComponentGroup _needToLoadNeighboursChunks;

        private Material _chunkMaterial;
        private Vector2Int _mapSize;
        private Dictionary<int3, Entity> _chunks;

        protected override void OnCreateManager()
        {
            _chunks = new Dictionary<int3, Entity>();
            //_allChunks = GetComponentGroup(typeof(RegularChunk));
            _worldSpawners = GetComponentGroup(typeof(MapParameters));
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
                            CreateChunk(new int3(x, 0, y));
                        }
                }

            }
        }

        private void CreateChunk(int3 pos)
        {
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(pos, _chunkMaterial);
            var ent = chunk.gameObject.AddComponent<GameObjectEntity>().Entity;
            EntityManager.AddComponentData(ent, new ChunkNeedTerrainGeneration());
            EntityManager.AddComponentData(ent, new ChunkPosComponent { Pos = new int3(pos) });

            var buf1 = EntityManager.AddBuffer<Voxel>(ent);
            //Debug.Log($"Length of voxel buffer: {buf1.Length} Capacity of voxel buffer: {buf1.Capacity}");
            buf1.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);
            //Debug.Log($"Length of voxel buffer: {buf1.Length} Capacity of voxel buffer: {buf1.Capacity}");

            var buf2 = EntityManager.AddBuffer<VoxelLightingLevel>(ent);
            //Debug.Log($"Length of light buffer: {buf2.Length} Capacity of light buffer: {buf2.Capacity}");
            buf2.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);
            //Debug.Log($"Length of light buffer: {buf2.Length} Capacity of light buffer: {buf2.Capacity}");

            EntityManager.AddBuffer<VoxelSetQueryData>(ent); // now voxels can be changed
            EntityManager.AddBuffer<LightSetQueryData>(ent); // now light can be changed

            var neighbs = new ChunkNeighboursComponent();

            _chunks.Add(pos, ent);
            for(int i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                var dirVec = dir.ToInt3();
                if(_chunks.ContainsKey(pos + dirVec))
                {
                    var nextEnt = _chunks[pos + dirVec];
                    var nextNeighb = EntityManager.GetComponentData<ChunkNeighboursComponent>(nextEnt);
                    nextNeighb[dir.Opposite()] = ent;
                    neighbs[dir] = nextEnt;
                    EntityManager.SetComponentData(nextEnt, nextNeighb);
                }
            }
            EntityManager.AddComponentData(ent, neighbs);
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
