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
        private Dictionary<Vector3Int, RegularChunk> _chunks;

        protected override void OnCreateManager()
        {
            _allChunks = EntityManager.CreateComponentGroup(typeof(RegularChunk));
            _worldSpawners = EntityManager.CreateComponentGroup(typeof(MapParameters));
        }

        protected override void OnDestroyManager()
        {
            var chunks = _allChunks.GetComponentArray<RegularChunk>();
            for(int i = 0; i < chunks.Length; i++)
            {
                chunks[i].Deinitialize();
            }
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
                            CreateChunk(new Vector3Int(x, y, 0));
                        }
                }

            }
        }

        private void CreateChunk(Vector3Int pos)
        {
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(pos, _chunkMaterial);
            var ent = chunk.gameObject.AddComponent<GameObjectEntity>().Entity;
            PostUpdateCommands.AddComponent(ent, new ChunkNeedTerrainGeneration());

            _chunks.Add(pos, chunk);
            for(int i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)i;
                var dirVec = dir.ToVecInt();
                if(_chunks.ContainsKey(pos + dirVec))
                {
                    chunk[dir] = _chunks[pos + dirVec];
                }
            }
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
