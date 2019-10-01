using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using World.Components;

namespace World.Systems.Regions
{
    public class FillRegionWithChunksSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem _barrier;

        private EntityQuery _entityQuery;

        protected override void OnCreate()
        {
            _barrier     = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _entityQuery = GetEntityQuery(ComponentType.ReadOnly<ChunkNeedAddToRegion>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var j1 = new FillJob
            {
                commandBuffer = _barrier.CreateCommandBuffer(),
                components =
                    _entityQuery.ToComponentDataArray<ChunkNeedAddToRegion>(Allocator.TempJob,
                        out var collectComponents),
                entities      = _entityQuery.ToEntityArray(Allocator.TempJob, out var collectEntities),
                chunksBuffers = GetBufferFromEntity<RegionChunks>()
            };
            var h1 = j1.Schedule(JobHandle.CombineDependencies(collectComponents, collectEntities, inputDeps));

            _barrier.AddJobHandleForProducer(h1);

            return h1;
        }

        private struct FillJob : IJob
        {
            public EntityCommandBuffer commandBuffer;

            public BufferFromEntity<RegionChunks> chunksBuffers;

            [DeallocateOnJobCompletion]
            public NativeArray<Entity> entities;

            [DeallocateOnJobCompletion]
            public NativeArray<ChunkNeedAddToRegion> components;

            public void Execute()
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var parent = components[i].parentRegion;
                    chunksBuffers[parent].Add(new RegionChunks {chunk = entities[i]});

                    commandBuffer.RemoveComponent<ChunkNeedAddToRegion>(entities[i]);
                    commandBuffer.AddComponent(entities[i], new ChunkNeedTerrainGeneration());
                }
            }
        }
    }
}