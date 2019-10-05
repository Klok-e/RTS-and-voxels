using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using World.Components;
using World.DynamicBuffers;
using World.Systems.ChunkHandling;
using Object = UnityEngine.Object;

namespace World.Systems.Regions
{
    public class RegionLoadUnloadSystem : ComponentSystem
    {
        private InitChunkTexturesMaterialsSystem _materials;

        private EntityCommandBufferSystem _barrier;

        private Queue<RegularChunk> _chunkPool;

        public NativeHashMap<int3, Entity> PosToChunkEntity { get; private set; }

        public Dictionary<int3, RegularChunk> PosToChunk { get; private set; }

        protected override void OnCreate()
        {
            PosToChunkEntity = new NativeHashMap<int3, Entity>(1000, Allocator.Persistent);
            PosToChunk       = new Dictionary<int3, RegularChunk>();

            _barrier = World.GetOrCreateSystem<EntityCommandBufferSystem>();

            _materials = World.GetOrCreateSystem<InitChunkTexturesMaterialsSystem>();
            _chunkPool = new Queue<RegularChunk>();
        }

        protected override void OnDestroy()
        {
            PosToChunkEntity.Dispose();
        }

        protected override void OnUpdate()
        {
            var commands = _barrier.CreateCommandBuffer();

            // load
            var needLoad = ComponentType.ReadOnly<RegionNeedLoadComponentTag>();
            Entities.WithAll(needLoad).ForEach((Entity ent, ref RegionPosComponent regionPos) =>
            {
                if (!TryFindRegion(regionPos.Pos))
                {
                    //Debug.Log($"Region {regionPos.Pos}");
                    PopulateRegion(regionPos.Pos, ent);

                    commands.RemoveComponent<RegionNeedLoadComponentTag>(ent);
                }
            });

            // unload
            var chunkPositions = GetComponentDataFromEntity<ChunkPosComponent>(true);

            var needUnload = ComponentType.ReadOnly<RegionNeedUnloadComponentTag>();
            var exNeedLoad = ComponentType.ReadOnly<RegionNeedLoadComponentTag>();
            Entities.WithNone(exNeedLoad).WithAll(needUnload).ForEach(
                (Entity regionEntity, DynamicBuffer<RegionChunks> chunks, ref RegionPosComponent regionPos) =>
                {
                    SaveRegion(regionPos.Pos);

                    // delete chunks
                    for (int i = 0; i < chunks.Length; i++)
                        DestroyChunk(chunkPositions[chunks[i].chunk].Pos, chunks[i].chunk);

                    // delete region
                    commands.DestroyEntity(regionEntity);
                });

            Entities.WithAll(typeof(ChunkQueuedForDeletionTag)).ForEach((Entity ent, ref ChunkPosComponent pos) =>
            {
                commands.DestroyEntity(ent);

                _chunkPool.Enqueue(PosToChunk[pos.Pos].Deinitialize());
                PosToChunk.Remove(pos.Pos);
                PosToChunkEntity.Remove(pos.Pos);
            });
        }

        private void PopulateRegion(int3 regionPos, Entity region)
        {
            for (int z = 0; z < VoxConsts.RegionSize; z++)
            for (int y = 0; y < VoxConsts.RegionSize; y++)
            for (int x = 0; x < VoxConsts.RegionSize; x++)
                CreateChunk(regionPos * VoxConsts.RegionSize + math.int3(x, y, z), region);
        }

        private void CreateChunk(int3 chunkPos, Entity region)
        {
            var ent = EntityManager.CreateEntity();

            // archetypes are for weak
            EntityManager.AddComponentData(ent, new ChunkNeedAddToRegion {parentRegion = region});
            EntityManager.AddComponentData(ent, new ChunkPosComponent
            {
                Pos = chunkPos
            });

            var buf1 = EntityManager.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxConsts.ChunkSize * VoxConsts.ChunkSize * VoxConsts.ChunkSize);

            var buf2 = EntityManager.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxConsts.ChunkSize * VoxConsts.ChunkSize * VoxConsts.ChunkSize);

            EntityManager.AddBuffer<VoxelSetQueryData>(ent);
            EntityManager.AddBuffer<LightSetQueryData>(ent);

            // create chunk object
            var chunk = _chunkPool.Count == 0 ? RegularChunk.CreateNew() : _chunkPool.Dequeue();

            chunk.Initialize(chunkPos, _materials.ChunkMaterial);

            //Debug.Log($"Chunk {chunkPos}");
            PosToChunk.Add(chunkPos, chunk);
            if (!PosToChunkEntity.TryAdd(chunkPos, ent))
                Debug.LogError("Could not add to PosToEntity hashmap");
        }

        private void DestroyChunk(int3 pos, Entity entity)
        {
            PostUpdateCommands.AddComponent(entity, new ChunkQueuedForDeletionTag());
        }

        // TODO: this
        private bool TryFindRegion(int3 pos)
        {
            return false;
        }

        // TODO: this, synchronously first then try async
        private void SaveRegion(int3 pos)
        {
        }
    }
}