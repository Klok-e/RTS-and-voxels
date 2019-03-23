using Scripts.Help;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using dirFlags = Scripts.Help.DirectionsHelper.BlockDirectionFlag;

namespace Scripts.World.Systems
{
    [UpdateBefore(typeof(ChunkMeshSystem))]
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

            public NativeQueue<Entity>.Concurrent ToBeRemeshed;

            public EntityArray Entities;

            public ComponentDataArray<ChunkNeighboursComponent> Neighbours;

            public void Execute(int index)
            {
                ApplyChangesToChunk(VoxelBuffers[index], VoxelSetQuery[index], Neighbours[index]);
                ToBeRemeshed.Enqueue(Entities[index]);
            }

            private void ApplyChangesToChunk(DynamicBuffer<Voxel> buffer, DynamicBuffer<VoxelSetQueryData> query, ChunkNeighboursComponent neighbs)
            {
                for(int i = 0; i < query.Length; i++)
                {
                    var x = query[i];

                    buffer.AtSet(x.Pos.x, x.Pos.y, x.Pos.z,
                        new Voxel
                        {
                            Type = x.NewVoxelType,
                        });

                    var dir = DirectionsHelper.AreCoordsOnBordersOfChunk(x.Pos);
                    if(dir != dirFlags.None)
                        for(int k = 0; k < 6; k++)
                        {
                            var diriter = (dirFlags)(1 << k);
                            var ent = neighbs[diriter];
                            if(ent != Entity.Null)
                                ToBeRemeshed.Enqueue(ent);
                        }
                }
                query.Clear();
            }
        }

        private struct ChangeTagsJob : IJob
        {
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly]
            public ComponentDataFromEntity<ChunkDirtyComponent> AlreadyDirty;

            public NativeQueue<Entity> ToBeRemeshed;

            public void Execute()
            {
                while(ToBeRemeshed.TryDequeue(out var ent))
                {
                    CommandBuffer.RemoveComponent<ChunkNeedApplyVoxelChanges>(ent);
                    if(!AlreadyDirty.Exists(ent))
                        CommandBuffer.AddComponent(ent, new ChunkDirtyComponent());
                }
            }
        }

        protected override void OnCreateManager()
        {
            _chunksNeedApplyVoxelChanges = GetComponentGroup(
                ComponentType.Create<ChunkNeedApplyVoxelChanges>(),
                ComponentType.Create<Voxel>(),
                ComponentType.Create<VoxelSetQueryData>(),
                ComponentType.Create<ChunkNeighboursComponent>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var entities = _chunksNeedApplyVoxelChanges.GetEntityArray();
            var entitiesToBeRemeshed = new NativeQueue<Entity>(Allocator.TempJob);
            var j1 = new ApplyChanges
            {
                VoxelBuffers = _chunksNeedApplyVoxelChanges.GetBufferArray<Voxel>(),
                VoxelSetQuery = _chunksNeedApplyVoxelChanges.GetBufferArray<VoxelSetQueryData>(),
                ToBeRemeshed = entitiesToBeRemeshed.ToConcurrent(),
                Entities = entities,
                Neighbours = _chunksNeedApplyVoxelChanges.GetComponentDataArray<ChunkNeighboursComponent>(),
            };
            var j2 = new ChangeTagsJob
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                AlreadyDirty = GetComponentDataFromEntity<ChunkDirtyComponent>(true),
                ToBeRemeshed = entitiesToBeRemeshed,
            };

            return j2.Schedule(j1.Schedule(entities.Length, 1, inputDeps));
        }
    }
}
