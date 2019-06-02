using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems.Regions
{
    public class RegionLoadUnloadSystem : ComponentSystem
    {
        public NativeHashMap<int3, Entity> PosToEntity { get; private set; }
        public Dictionary<int3, RegularChunk> PosToChunk { get; private set; }

        private InitChunkTexturesMaterialsSystem _materials;

        protected override void OnCreate()
        {
            PosToEntity = new NativeHashMap<int3, Entity>(10000, Allocator.Persistent);
            PosToChunk = new Dictionary<int3, RegularChunk>();

            _materials = World.GetOrCreateSystem<InitChunkTexturesMaterialsSystem>();
        }

        protected override void OnDestroy()
        {
            PosToEntity.Dispose();
        }

        protected override void OnUpdate()
        {
            // TODO: ineffieient
            // refresh
            PosToEntity.Clear();
            Entities.ForEach((Entity entity, ref ChunkPosComponent pos) =>
            {
                if(!PosToEntity.TryAdd(pos.Pos, entity))
                    throw new System.Exception("Could not add to PosToEntity hashmap");
            });

            // load
            Entities.WithAll(typeof(RegionNeedLoadComponentTag)).ForEach((Entity ent, ref RegionPosComponent regionPos) =>
            {
                if(!TryFindRegion(regionPos.Pos))
                {
                    //Debug.Log($"Region {regionPos.Pos}");
                    PopulateRegion(regionPos.Pos, ent);

                    PostUpdateCommands.RemoveComponent<RegionNeedLoadComponentTag>(ent);
                }
                else
                {
                    // TODO: this
                }
            });

            // unload
            var chunkPositions = GetComponentDataFromEntity<ChunkPosComponent>(true);
            Entities.WithAll<RegionNeedUnloadComponentTag>().ForEach((Entity regionEntity, DynamicBuffer<RegionChunks> chunks, ref RegionPosComponent regionPos) =>
            {
                SaveRegion(regionPos.Pos);

                // delete chunks
                for(int i = 0; i < chunks.Length; i++)
                    DestroyChunk(chunkPositions[chunks[i].Chunk].Pos, chunks[i].Chunk);

                // delete region
                PostUpdateCommands.DestroyEntity(regionEntity);
            });
        }

        private void PopulateRegion(int3 regionPos, Entity region)
        {
            for(int z = 0; z < VoxConsts._regionSize; z++)
                for(int y = 0; y < VoxConsts._regionSize; y++)
                    for(int x = 0; x < VoxConsts._regionSize; x++)
                    {
                        CreateChunk(regionPos * VoxConsts._regionSize + math.int3(x, y, z), region);
                    }
        }

        private void CreateChunk(int3 chunkPos, Entity region)
        {
            var ent = PostUpdateCommands.CreateEntity();

            // archetypes are for weak
            PostUpdateCommands.AddComponent(ent, new ChunkNeedAddToRegion { ParentRegion = region, });
            PostUpdateCommands.AddComponent(ent, new ChunkPosComponent
            {
                Pos = chunkPos,
            });

            var buf1 = PostUpdateCommands.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            var buf2 = PostUpdateCommands.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            PostUpdateCommands.AddBuffer<VoxelSetQueryData>(ent);
            PostUpdateCommands.AddBuffer<LightSetQueryData>(ent);

            // create chunk object
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(chunkPos, _materials._chunkMaterial);

            //Debug.Log($"Chunk {chunkPos}");
            PosToChunk.Add(chunkPos, chunk);
        }

        private void DestroyChunk(int3 pos, Entity entity)
        {
            PostUpdateCommands.DestroyEntity(entity);

            UnityEngine.Object.Destroy(PosToChunk[pos].gameObject);
            PosToChunk.Remove(pos);
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
