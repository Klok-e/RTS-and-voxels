using Scripts.World.Components;
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
    //public class RegionRemoveSystem : JobComponentSystem
    //{
    //    private EndSimulationEntityCommandBufferSystem _barrier;
    //
    //    private struct RemoveRegionsJob : IJobForEachWithEntity<RegionPosComponent>
    //    {
    //        [DeallocateOnJobCompletion]
    //        public NativeArray<int3> RegionsLoadersIn;
    //
    //        public int RegionDistance;
    //
    //        public EntityCommandBuffer.Concurrent Buffer;
    //
    //        public void Execute(Entity entity, int index, ref RegionPosComponent c0)
    //        {
    //            for(int i = 0; i < RegionsLoadersIn.Length; i++)
    //            {
    //                if(math.distance(RegionsLoadersIn[i], c0.Pos) > RegionDistance + 1)
    //                    Buffer.AddComponent(index, entity, new RegionNeedUnloadComponentTag());
    //            }
    //        }
    //    }
    //
    //    protected override void OnCreate()
    //    {
    //        _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    //    }
    //
    //    protected override JobHandle OnUpdate(JobHandle inputDeps)
    //    {
    //        var j1 = new RemoveRegionsJob
    //        {
    //            Buffer = _barrier.CreateCommandBuffer().ToConcurrent(),
    //
    //        };
    //
    //        var h1 = j1.Schedule(this, inputDeps);
    //
    //        _barrier.AddJobHandleForProducer(h1);
    //
    //        return h1;
    //    }
    //}
}
