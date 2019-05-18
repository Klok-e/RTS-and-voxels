using Scripts.World;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Scripts.World.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    [DisableAutoCreation]
    public class RemeshPerformanceTestSystem : JobComponentSystem
    {
        private EntityCommandBufferSystem _barrier;

        [BurstCompile]
        private struct RandomlySetVoxelsJob : IJobForEachWithEntity_EB<VoxelSetQueryData>
        {
            public EntityCommandBuffer.Concurrent CommandBuffer;

            public Unity.Mathematics.Random Rand;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkNeedApplyVoxelChanges> NeedApplChanges;

            public void Execute(Entity entity, int index, DynamicBuffer<VoxelSetQueryData> buf)
            {
                if(Rand.NextFloat(0f, 1f) > 0.9f)
                {
                    buf.Add(new VoxelSetQueryData
                    {
                        NewVoxelType = Rand.NextBool() ? VoxelType.Empty : VoxelType.Dirt,
                        Pos = Rand.NextInt3(new int3(0), new int3(VoxConsts._chunkSize)),
                    });
                    if(!NeedApplChanges.Exists(entity))
                        CommandBuffer.AddComponent(index, entity, new ChunkNeedApplyVoxelChanges());
                }
            }
        }

        protected override void OnCreateManager()
        {
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var j1 = new RandomlySetVoxelsJob
            {
                Rand = new Unity.Mathematics.Random(math.asuint(Time.time)),
                CommandBuffer = _barrier.CreateCommandBuffer().ToConcurrent(),
                NeedApplChanges = GetComponentDataFromEntity<ChunkNeedApplyVoxelChanges>(true),
            };
            var h1 = j1.Schedule(this, inputDeps);

            _barrier.AddJobHandleForProducer(h1);

            return h1;
        }
    }
}
