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

namespace Assets.Scripts.World.Systems
{
    public class RemeshPerformanceTestSystem : JobComponentSystem
    {
        private ComponentGroup _readyChunks;

        private class RemeshPerformanceTestBarrier : BarrierSystem { }

        [Inject]
        private EndFrameBarrier _barrier;

        private struct RandomlySetVoxelsJob : IJobParallelFor
        {
            public EntityCommandBuffer.Concurrent CommandBuffer;

            public BufferArray<VoxelSetQueryData> Buffers;

            public Unity.Mathematics.Random Rand;

            public EntityArray Entities;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkNeedApplyVoxelChanges> NeedApplChanges;

            public void Execute(int index)
            {
                var buf = Buffers[index];
                if(Rand.NextFloat(0f, 1f) > 0.9f)
                {
                    buf.Add(new VoxelSetQueryData
                    {
                        NewVoxelType = Rand.NextBool() ? VoxelType.Empty : VoxelType.Dirt,
                        Pos = Rand.NextInt3(new int3(0), new int3(VoxConsts._chunkSize))
                    });
                    if(!NeedApplChanges.Exists(Entities[index]))
                        CommandBuffer.AddComponent(index, Entities[index], new ChunkNeedApplyVoxelChanges());
                }
            }
        }

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _readyChunks = GetComponentGroup(
                ComponentType.Create<VoxelSetQueryData>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var j1 = new RandomlySetVoxelsJob
            {
                Buffers = _readyChunks.GetBufferArray<VoxelSetQueryData>(),
                Rand = new Unity.Mathematics.Random(math.asuint(Time.time)),
                CommandBuffer = _barrier.CreateCommandBuffer().ToConcurrent(),
                Entities = _readyChunks.GetEntityArray(),
                NeedApplChanges = GetComponentDataFromEntity<ChunkNeedApplyVoxelChanges>(true),
            };

            return j1.Schedule(j1.Buffers.Length, 1, inputDeps);
        }
    }
}
