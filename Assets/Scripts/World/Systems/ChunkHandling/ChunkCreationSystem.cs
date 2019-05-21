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
        public NativeHashMap<int3, Entity> PosToEntity { get; private set; }

        public Dictionary<int3, RegularChunk> PosToChunk { get; private set; }

        private int3 _loaderChunkInPrev;

        private InitChunkTexturesMaterialsSystem _materials;

        protected override void OnCreateManager()
        {
            PosToEntity = new NativeHashMap<int3, Entity>(10000, Allocator.Persistent);
            PosToChunk = new Dictionary<int3, RegularChunk>();

            _materials = World.GetOrCreateSystem<InitChunkTexturesMaterialsSystem>();
        }

        protected override void OnDestroyManager()
        {
            PosToEntity.Dispose();
        }

        protected override void OnUpdate()
        {
            PosToEntity.Clear();
            Entities.ForEach((Entity entity, ref ChunkPosComponent pos) =>
            {
                if(!PosToEntity.TryAdd(pos.Pos, entity))
                    throw new System.Exception("Could not add to PosToEntity hashmap");
            });

            Entities.ForEach((ref MapLoader loader, ref LocalToWorld pos) =>
            {
                var loaderChunkIn = VoxConsts.ChunkIn(pos.Position);
                if(math.any(_loaderChunkInPrev != loaderChunkIn))
                {
                    _loaderChunkInPrev = loaderChunkIn;

                    // gen new
                    for(int x = -loader.RegionDistance; x <= loader.RegionDistance; x++)
                        for(int y = -loader.RegionDistance / 2; y <= loader.RegionDistance / 2; y++)
                            for(int z = -loader.RegionDistance; z <= loader.RegionDistance; z++)
                            {
                                var chPos = new int3(x, y, z) + loaderChunkIn;
                                if(!PosToEntity.TryGetValue(chPos, out var _))
                                    CreateChunk(chPos);
                            }

                    // prune old
                    using(var keys = PosToEntity.GetKeyArray(Allocator.Temp))
                        foreach(var key in keys)
                        {
                            if(math.distance(loaderChunkIn, key) > loader.RegionDistance * 2)
                                RemoveChunk(key);
                        }
                }
            });
        }

        private void RemoveChunk(int3 pos)
        {
            var ent = PosToEntity[pos];
            PostUpdateCommands.DestroyEntity(ent);

            Object.Destroy(PosToChunk[pos]);
            PosToChunk.Remove(pos);
        }

        private (RegularChunk, Entity) CreateChunk(int3 pos)
        {
            var chunk = RegularChunk.CreateNew();
            chunk.Initialize(pos, _materials._chunkMaterial);

            PosToChunk.Add(pos, chunk);

            var ent = PostUpdateCommands.CreateEntity();

            PostUpdateCommands.AddComponent(ent, new ChunkNeedTerrainGeneration());
            PostUpdateCommands.AddComponent(ent, new ChunkPosComponent { Pos = pos, });

            var buf1 = PostUpdateCommands.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            var buf2 = PostUpdateCommands.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            PostUpdateCommands.AddBuffer<VoxelSetQueryData>(ent); // now voxels can be changed
            PostUpdateCommands.AddBuffer<LightSetQueryData>(ent); // now light can be changed

            return (chunk, ent);
        }
    }
}
