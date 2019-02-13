using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Scripts.World.Systems
{
    [UpdateBefore(typeof(ChunkSystem))]
    public class ApplyVoxelChangesSystem : JobComponentSystem
    {
        private ComponentGroup _chunksNeedApplyVoxelChanges;

        [Inject]
        private EndFrameBarrier _barrier;

        private class ApplyVoxelsBarrier : BarrierSystem { }

        [BurstCompile]
        private struct ApplyChanges : IJobParallelFor
        {
            [WriteOnly]
            public BufferArray<Voxel> VoxelBuffers;

            public BufferArray<VoxelSetQueryData> VoxelSetQuery;

            public void Execute(int index)
            {
                ApplyChangesToChunk(VoxelBuffers[index], VoxelSetQuery[index]);
            }

            private void ApplyChangesToChunk(DynamicBuffer<Voxel> buffer, DynamicBuffer<VoxelSetQueryData> query)
            {
                for(int i = 0; i < query.Length; i++)
                {
                    var x = query[i];

                    buffer.AtSet(x.Pos.x, x.Pos.y, x.Pos.z,
                        new Voxel
                        {
                            Type = x.NewVoxelType,
                        });
                }
                query.Clear();
            }
        }

        private struct ChangeTagsJob : IJob
        {
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly]
            public ComponentDataFromEntity<ChunkDirtyComponent> AlreadyDirty;
            public EntityArray Entities;

            public void Execute()
            {
                for(int index = 0; index < Entities.Length; index++)
                {
                    CommandBuffer.RemoveComponent<ChunkNeedApplyVoxelChanges>(Entities[index]);
                    if(!AlreadyDirty.Exists(Entities[index]))
                        CommandBuffer.AddComponent(Entities[index], new ChunkDirtyComponent());
                }
            }
        }

        protected override void OnCreateManager()
        {
            _chunksNeedApplyVoxelChanges = GetComponentGroup(
                ComponentType.Create<ChunkNeedApplyVoxelChanges>(),
                ComponentType.Create<Voxel>(),
                ComponentType.Create<VoxelSetQueryData>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var ents = _chunksNeedApplyVoxelChanges.GetEntityArray();
            var j1 = new ApplyChanges
            {
                VoxelBuffers = _chunksNeedApplyVoxelChanges.GetBufferArray<Voxel>(),
                VoxelSetQuery = _chunksNeedApplyVoxelChanges.GetBufferArray<VoxelSetQueryData>(),
            };
            var j2 = new ChangeTagsJob
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                Entities = ents,
                AlreadyDirty = GetComponentDataFromEntity<ChunkDirtyComponent>(true),
            };

            return JobHandle.CombineDependencies(
                j2.Schedule(inputDeps),
                j1.Schedule(ents.Length, 1, inputDeps));
        }
    }
}
