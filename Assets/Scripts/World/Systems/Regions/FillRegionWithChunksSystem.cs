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

namespace Scripts.World.Systems.Regions
{
    public class FillRegionWithChunksSystem : JobComponentSystem
    {
        private struct FillJob : IJob
        {
            public EntityCommandBuffer CommandBuffer;

            public BufferFromEntity<RegionChunks> ChunksBuffers;

            [DeallocateOnJobCompletion]
            public NativeArray<Entity> Entities;

            [DeallocateOnJobCompletion]
            public NativeArray<ChunkNeedAddToRegion> Components;

            public void Execute()
            {
                for(int i = 0; i < Entities.Length; i++)
                {
                    var parent = Components[i].ParentRegion;
                    ChunksBuffers[parent].Add(new RegionChunks { Chunk = Entities[i], });

                    CommandBuffer.RemoveComponent<ChunkNeedAddToRegion>(Entities[i]);
                    CommandBuffer.AddComponent(Entities[i], new ChunkNeedTerrainGeneration());
                }
            }
        }

        private EndSimulationEntityCommandBufferSystem _barrier;

        private EntityQuery _entityQuery;

        protected override void OnCreate()
        {
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _entityQuery = GetEntityQuery(ComponentType.ReadOnly<ChunkNeedAddToRegion>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var j1 = new FillJob
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                Components = _entityQuery.ToComponentDataArray<ChunkNeedAddToRegion>(Allocator.TempJob, out var collectComponents),
                Entities = _entityQuery.ToEntityArray(Allocator.TempJob, out var collectEntities),
                ChunksBuffers = GetBufferFromEntity<RegionChunks>(),
            };
            var h1 = j1.Schedule(JobHandle.CombineDependencies(collectComponents, collectEntities, inputDeps));

            _barrier.AddJobHandleForProducer(h1);

            return h1;
        }
    }
}
