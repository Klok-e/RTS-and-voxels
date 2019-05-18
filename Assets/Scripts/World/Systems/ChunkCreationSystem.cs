using Scripts.Help;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class ChunkCreationSystem : ComponentSystem
    {
        private Dictionary<int3, Entity> _chunks;

        private int3 _loaderChunkInPrev;

        private InitChunkTexturesMaterialsSystem _materials;

        protected override void OnCreateManager()
        {
            _chunks = new Dictionary<int3, Entity>();

            _materials = World.GetOrCreateSystem<InitChunkTexturesMaterialsSystem>();
        }

        protected override void OnDestroyManager()
        {
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((ref MapLoader loader, ref Translation pos) =>
            {
                var loaderChunkIn = ChunkIn(pos);
                if(math.any(_loaderChunkInPrev != loaderChunkIn))
                {
                    _loaderChunkInPrev = loaderChunkIn;
                    // gen new
                    for(int x = -loader.ChunkDistance; x <= loader.ChunkDistance; x++)
                        for(int y = -loader.ChunkDistance / 2; y <= loader.ChunkDistance / 2; y++)
                            for(int z = -loader.ChunkDistance; z <= loader.ChunkDistance; z++)
                            {
                                var chPos = new int3(x, y, z) + loaderChunkIn;
                                if(!_chunks.ContainsKey(chPos))
                                    CreateChunk(chPos);
                            }

                    var remove = new List<int3>();
                    // prune old
                    foreach(var key in _chunks.Keys)
                    {
                        if(math.distance(loaderChunkIn, key) > loader.ChunkDistance * 2)
                        {
                            RemoveChunk(key);
                            remove.Add(key);
                        }
                    }
                    foreach(var item in remove)
                    {
                        _chunks.Remove(item);
                    }
                }
            });
        }

        private int3 ChunkIn(Translation pos)
        {
            var worldPos = pos.Value / VoxConsts._blockSize;
            var loaderChunkInf = (worldPos - (math.float3(1f) * (VoxConsts._chunkSize / 2))) / VoxConsts._chunkSize;
            var loaderChunkIn = new int3((int)math.round(loaderChunkInf.x), (int)math.round(loaderChunkInf.y), (int)math.round(loaderChunkInf.z));
            return loaderChunkIn;
        }

        private void RemoveChunk(int3 pos)
        {
            var ent = _chunks[pos];
            Object.Destroy(EntityManager.GetComponentObject<RegularChunk>(ent).gameObject, 1f);
            PostUpdateCommands.DestroyEntity(ent);

            for(int i = 0; i < 6; i++)
            {
                var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                var dirVec = dir.ToInt3();
                if(_chunks.ContainsKey(pos + dirVec))
                {
                    var nextEnt = _chunks[pos + dirVec];
                    var nextNeighb = EntityManager.GetComponentData<ChunkNeighboursComponent>(nextEnt);
                    nextNeighb[dir.Opposite()] = Entity.Null;
                    EntityManager.SetComponentData(nextEnt, nextNeighb);
                }
            }
        }

        private void CreateChunk(int3 pos)
        {
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(pos, _materials._chunkMaterial);
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
                    if(!EntityManager.HasComponent<ChunkDirtyComponent>(nextEnt) && !EntityManager.HasComponent<ChunkNeedTerrainGeneration>(nextEnt))
                        EntityManager.AddComponentData(nextEnt, new ChunkDirtyComponent());
                }
            }
            EntityManager.AddComponentData(ent, neighbs);
        }
    }
}
