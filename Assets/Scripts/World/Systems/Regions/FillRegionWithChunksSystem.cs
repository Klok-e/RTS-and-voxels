using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Scripts.World.Systems.Regions
{
    public class FillRegionWithChunksSystem : JobComponentSystem
    {
        /*
        [RequireComponentTag(typeof(RegionNeedFillWithChunksComponentTag))]
        private struct FillJob : IJobForEachWithEntity_EBC<RegionChunks, RegionPosComponent>
        {
            public EntityCommandBuffer.Concurrent CommandBuffer;

            public void Execute(Entity entity, int index, DynamicBuffer<RegionChunks> b0, ref RegionPosComponent pos)
            {
                CommandBuffer.RemoveComponent<RegionNeedFillWithChunksComponentTag>(index, entity);

                for(int z = 0; z < VoxConsts._regionSize; z++)
                    for(int y = 0; y < VoxConsts._regionSize; y++)
                        for(int x = 0; x < VoxConsts._regionSize; x++)
                        {

                        }
            }
        }
        */

        private EndSimulationEntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //var j1 = new FillJob
            //{
            //    CommandBuffer = _barrier.CreateCommandBuffer().ToConcurrent(),
            //};
            //var h1 = j1.Schedule(this, inputDeps);
            return inputDeps;
        }
    }
}
