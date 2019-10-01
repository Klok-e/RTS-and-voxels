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

        public NativeHashMap<int3, Entity> PosToChunkEntity { get; private set; }

        public Dictionary<int3, RegularChunk> PosToChunk { get; private set; }

        protected override void OnCreate()
        {
            PosToChunkEntity = new NativeHashMap<int3, Entity>(1000, Allocator.Persistent);
            PosToChunk       = new Dictionary<int3, RegularChunk>();

            _materials = World.GetOrCreateSystem<InitChunkTexturesMaterialsSystem>();
        }

        protected override void OnDestroy()
        {
            PosToChunkEntity.Dispose();
        }

        protected override void OnUpdate()
        {
            // TODO: ineffieient
            // refresh
            PosToChunkEntity.Clear();
            Entities.ForEach((Entity entity, ref ChunkPosComponent pos) =>
            {
                if (!PosToChunkEntity.TryAdd(pos.Pos, entity))
                    throw new Exception("Could not add to PosToEntity hashmap");
            });

            // load
            var needLoad = ComponentType.ReadOnly<RegionNeedLoadComponentTag>();
            Entities.WithAll(needLoad).ForEach((Entity ent, ref RegionPosComponent regionPos) =>
            {
                if (!TryFindRegion(regionPos.Pos))
                {
                    //Debug.Log($"Region {regionPos.Pos}");
                    PopulateRegion(regionPos.Pos, ent);

                    PostUpdateCommands.RemoveComponent<RegionNeedLoadComponentTag>(ent);
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
                    PostUpdateCommands.DestroyEntity(regionEntity);
                });

            Entities.WithAll(typeof(ChunkQueuedForDeletionTag)).ForEach((Entity ent, ref ChunkPosComponent pos) =>
            {
                PostUpdateCommands.DestroyEntity(ent);

                Object.Destroy(PosToChunk[pos.Pos].gameObject);
                PosToChunk.Remove(pos.Pos);
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
            var ent = PostUpdateCommands.CreateEntity();

            // archetypes are for weak
            PostUpdateCommands.AddComponent(ent, new ChunkNeedAddToRegion {parentRegion = region});
            PostUpdateCommands.AddComponent(ent, new ChunkPosComponent
            {
                Pos = chunkPos
            });

            var buf1 = PostUpdateCommands.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxConsts.ChunkSize * VoxConsts.ChunkSize * VoxConsts.ChunkSize);

            var buf2 = PostUpdateCommands.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxConsts.ChunkSize * VoxConsts.ChunkSize * VoxConsts.ChunkSize);

            PostUpdateCommands.AddBuffer<VoxelSetQueryData>(ent);
            PostUpdateCommands.AddBuffer<LightSetQueryData>(ent);
            
            Debug.Log(ent);
            
           //PostUpdateCommands.Playback(EntityManager);

            // create chunk object
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(chunkPos, _materials.ChunkMaterial);

            //Debug.Log($"Chunk {chunkPos}");
            PosToChunk.Add(chunkPos, chunk);
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