﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using World.Components;
using World.DynamicBuffers;
using Random = Unity.Mathematics.Random;

namespace World.Systems.ChunkHandling
{
    [DisableAutoCreation]
    public class RemeshPerformanceTestSystem : JobComponentSystem
    {
        private EntityCommandBufferSystem _barrier;

        protected override void OnCreateManager()
        {
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var j1 = new RandomlySetVoxelsJob
            {
                rand            = new Random(math.asuint(Time.time)),
                commandBuffer   = _barrier.CreateCommandBuffer().ToConcurrent(),
                needApplChanges = GetComponentDataFromEntity<ChunkNeedApplyVoxelChanges>(true)
            };
            var h1 = j1.Schedule(this, inputDeps);

            _barrier.AddJobHandleForProducer(h1);

            return h1;
        }

        [BurstCompile]
        private struct RandomlySetVoxelsJob : IJobForEachWithEntity_EB<VoxelSetQueryData>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;

            public Random rand;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkNeedApplyVoxelChanges> needApplChanges;

            public void Execute(Entity entity, int index, DynamicBuffer<VoxelSetQueryData> buf)
            {
                if (rand.NextFloat(0f, 1f) > 0.9f)
                {
                    buf.Add(new VoxelSetQueryData
                    {
                        NewVoxelType = rand.NextBool() ? VoxelType.Empty : VoxelType.Dirt,
                        Pos          = rand.NextInt3(new int3(0), new int3(VoxConsts.ChunkSize))
                    });
                    if (!needApplChanges.Exists(entity))
                        commandBuffer.AddComponent(index, entity, new ChunkNeedApplyVoxelChanges());
                }
            }
        }
    }
}