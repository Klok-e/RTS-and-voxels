using Scripts.Help;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using dirFlags = Scripts.Help.DirectionsHelper.BlockDirectionFlag;

namespace Scripts.World.Systems
{
    [UpdateBefore(typeof(ChunkMeshSystem))]
    public class ApplyVoxelChangesSystem : JobComponentSystem
    {
        private ComponentGroup _chunksNeedApplyVoxelChanges;

        private NativeQueue<Entity> _toBeRemeshedCached;

        private NativeQueue<int3> _toPropRegLightCached;
        private NativeQueue<int3> _toPropSunLightCached;
        private NativeQueue<int3> _toDepRegLightCached;
        private NativeQueue<int3> _toDepSunLightCached;
        private NativeQueue<Entity> _toChangeLightCached;

        [Inject]
        private EndFrameBarrier _barrier;

        private class ApplyVoxelsBarrier : BarrierSystem { }

        [BurstCompile]
        private struct ApplyChangesVoxel : IJobParallelFor
        {
            [WriteOnly]
            public BufferArray<Voxel> VoxelBuffers;

            public BufferArray<VoxelSetQueryData> VoxelSetQuery;

            public NativeQueue<Entity>.Concurrent ToBeRemeshedNeighbs;

            public EntityArray Entities;

            [ReadOnly]
            public ComponentDataArray<ChunkNeighboursComponent> Neighbours;

            public void Execute(int index)
            {
                ApplyChangesToChunk(VoxelBuffers[index], VoxelSetQuery[index], Neighbours[index]);
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

                    var dir = DirectionsHelper.AreCoordsAtBordersOfChunk(x.Pos);
                    if(dir != dirFlags.None)
                        for(int k = 0; k < 6; k++)
                        {
                            var diriter = (dirFlags)(1 << k);
                            if((diriter & dir) != dirFlags.None)
                            {
                                var ent = neighbs[diriter];
                                if(ent != Entity.Null)
                                    ToBeRemeshedNeighbs.Enqueue(ent);
                            }
                        }
                }
                query.Clear();
            }
        }

        [BurstCompile]
        private struct ApplyChangesLight : IJob
        {
            public NativeQueue<int3> ToPropRegLight;
            public NativeQueue<int3> ToPropSunLight;
            public NativeQueue<int3> ToDepRegLight;
            public NativeQueue<int3> ToDepSunLight;
            public NativeQueue<Entity> ToChangeLight;

            public NativeQueue<Entity> ToBeRemeshed;

            [ReadOnly]
            public BufferFromEntity<Voxel> VoxelBuffers;
            public BufferFromEntity<VoxelLightingLevel> LightBuffers;

            public BufferFromEntity<LightSetQueryData> LightSetQuery;

            [ReadOnly]
            public EntityArray Entities;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkNeighboursComponent> Neighbours;

            public void Execute()
            {
                for(int i = 0; i < Entities.Length; i++)
                {
                    var lightSet = LightSetQuery[Entities[i]];
                    if(lightSet.Length > 0)
                        ToChangeLight.Enqueue(Entities[i]);
                }

                while(ToChangeLight.Count > 0)
                {
                    var ent = ToChangeLight.Dequeue();
                    ToBeRemeshed.Enqueue(ent);

                    var currLight = LightBuffers[ent];
                    var currSet = LightSetQuery[ent];
                    var currVox = VoxelBuffers[ent];

                    for(int i = 0; i < currSet.Length; i++)
                    {
                        var curr = currSet[i];

                        var p = curr.Pos;
                        var atLight = currLight.AtGet(p.x, p.y, p.z);
                        var newLvl = curr.NewLight;
                        if(curr.Propagation == PropagationType.Regular)
                        {
                            if(curr.LightType == SetLightType.RegularLight)
                            {
                                if(atLight.RegularLight < newLvl)
                                {
                                    ToPropRegLight.Enqueue(curr.Pos);
                                    currLight.AtSet(p.x, p.y, p.z, new VoxelLightingLevel(newLvl, atLight.Sunlight));
                                }
                                else if(atLight.RegularLight >= newLvl)
                                    ToDepRegLight.Enqueue(curr.Pos);
                            }
                            else
                            {
                                if(atLight.Sunlight < newLvl)
                                {
                                    ToPropSunLight.Enqueue(curr.Pos);
                                    currLight.AtSet(p.x, p.y, p.z, new VoxelLightingLevel(atLight.RegularLight, newLvl));
                                }
                                else if(atLight.Sunlight >= newLvl)
                                    ToDepSunLight.Enqueue(curr.Pos);
                            }
                        }
                        else
                        {
                            if(curr.Propagation == PropagationType.Depropagate)
                            {
                                if(curr.LightType == SetLightType.RegularLight)
                                {
                                    ToDepRegLight.Enqueue(curr.Pos);
                                }
                                else
                                {
                                    ToDepSunLight.Enqueue(curr.Pos);
                                }
                            }
                            else
                            {
                                if(curr.LightType == SetLightType.RegularLight)
                                {
                                    ToPropRegLight.Enqueue(curr.Pos);
                                }
                                else
                                {
                                    ToPropSunLight.Enqueue(curr.Pos);
                                }
                            }
                        }
                    }
                    currSet.Clear();

                    // Depropagate regular light
                    while(ToDepRegLight.Count > 0)
                    {
                        var depr = ToDepRegLight.Dequeue();

                        var lightAtPos = currLight.AtGet(depr.x, depr.y, depr.z);

                        currLight.AtSet(depr.x, depr.y, depr.z, new VoxelLightingLevel(0, lightAtPos.Sunlight));

                        // add neighbours to toDepReg
                        for(int i = 0; i < 6; i++)
                        {
                            var dir = (dirFlags)(1 << i);
                            var vec = dir.ToInt3();

                            var nextBlock = depr + vec;

                            var dirWrp = DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y, ref nextBlock.z);
                            if(dirWrp == dirFlags.None)
                            {
                                if(currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).RegularLight < lightAtPos.RegularLight) // less than supposed to be
                                    ToDepRegLight.Enqueue(nextBlock);
                                else
                                    ToPropRegLight.Enqueue(nextBlock);
                            }
                            else // not in this chunk
                            {
                                var nextEnt = Neighbours[ent][dir];
                                if(nextEnt != Entity.Null)
                                {
                                    var nextLight = LightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if(nextLight.RegularLight > 0)
                                    {
                                        PropagationType pr;
                                        if(nextLight.RegularLight < lightAtPos.RegularLight)
                                            pr = PropagationType.Depropagate; // depropagate
                                        else
                                            pr = PropagationType.Propagate; // propagate

                                        LightSetQuery[nextEnt].Add(new LightSetQueryData
                                        {
                                            LightType = SetLightType.RegularLight,
                                            Pos = nextBlock,
                                            Propagation = pr,
                                        });
                                        ToChangeLight.Enqueue(nextEnt);
                                    }
                                }
                            }
                        }
                    }

                    // Propagate regular light
                    while(ToPropRegLight.Count > 0)
                    {
                        var prop = ToPropRegLight.Dequeue();

                        var lightAtPos = currLight.AtGet(prop.x, prop.y, prop.z);

                        if(lightAtPos.RegularLight > 0)
                        {
                            // propagate to neighbours
                            for(int i = 0; i < 6; i++)
                            {
                                var dir = (dirFlags)(1 << i);
                                var vec = dir.ToInt3();

                                var nextBlock = prop + vec;

                                var dirWrp = DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y, ref nextBlock.z);
                                if(dirWrp == dirFlags.None)
                                {
                                    var nLight = currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if(nLight.RegularLight < lightAtPos.RegularLight - 1 // less than current
                                        &&
                                        currVox.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).Type.IsEmpty()) // and empty
                                    {
                                        ToPropRegLight.Enqueue(nextBlock);
                                        currLight.AtSet(nextBlock.x, nextBlock.y, nextBlock.z, new VoxelLightingLevel(lightAtPos.RegularLight - 1, nLight.Sunlight));
                                    }
                                }
                                else // not in this chunk
                                {
                                    var nextEnt = Neighbours[ent][dir];
                                    if(nextEnt != Entity.Null)
                                    {
                                        var nLight = LightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                        if(nLight.RegularLight < lightAtPos.RegularLight - 1 // less than current
                                            &&
                                            VoxelBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z).Type.IsEmpty()) // and empty
                                        {
                                            LightSetQuery[nextEnt].Add(new LightSetQueryData
                                            {
                                                LightType = SetLightType.RegularLight,
                                                Pos = nextBlock,
                                                Propagation = PropagationType.Propagate,
                                            });
                                            ToChangeLight.Enqueue(nextEnt);
                                            LightBuffers[nextEnt].AtSet(nextBlock.x, nextBlock.y, nextBlock.z, new VoxelLightingLevel(lightAtPos.RegularLight - 1, nLight.Sunlight));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private struct ChangeTagsJob : IJob
        {
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly]
            public ComponentDataFromEntity<ChunkDirtyComponent> AlreadyDirty;

            public NativeQueue<Entity> ToBeRemeshedNeighb;

            public EntityArray ToBeRemeshed;

            public void Execute()
            {
                // used as a hash map
                var _processedEntities = new NativeHashMap<Entity, int>(ToBeRemeshedNeighb.Count + ToBeRemeshed.Length, Allocator.Temp);

                for(int i = 0; i < ToBeRemeshed.Length; i++)
                {
                    CommandBuffer.RemoveComponent<ChunkNeedApplyVoxelChanges>(ToBeRemeshed[i]);
                    if(!AlreadyDirty.Exists(ToBeRemeshed[i]))
                        CommandBuffer.AddComponent(ToBeRemeshed[i], new ChunkDirtyComponent());
                    _processedEntities.TryAdd(ToBeRemeshed[i], 0);
                }

                while(ToBeRemeshedNeighb.TryDequeue(out var ent))
                {
                    if(!_processedEntities.TryGetValue(ent, out int _))
                    {
                        if(!AlreadyDirty.Exists(ent))
                            CommandBuffer.AddComponent(ent, new ChunkDirtyComponent());
                        _processedEntities.TryAdd(ent, 0);
                    }
                }
                _processedEntities.Dispose();
            }
        }

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _chunksNeedApplyVoxelChanges = GetComponentGroup(
                ComponentType.Create<ChunkNeedApplyVoxelChanges>(),
                ComponentType.Create<Voxel>(),
                ComponentType.Create<VoxelSetQueryData>(),
                ComponentType.Create<ChunkNeighboursComponent>(),
                ComponentType.Create<LightSetQueryData>());
            _toBeRemeshedCached = new NativeQueue<Entity>(Allocator.Persistent);
            _toPropRegLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toPropSunLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toDepRegLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toDepSunLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toChangeLightCached = new NativeQueue<Entity>(Allocator.Persistent);
        }

        protected override void OnDestroyManager()
        {
            base.OnDestroyManager();
            _toBeRemeshedCached.Dispose();
            _toPropRegLightCached.Dispose();
            _toPropSunLightCached.Dispose();
            _toDepRegLightCached.Dispose();
            _toDepSunLightCached.Dispose();
            _toChangeLightCached.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            _toBeRemeshedCached.Clear();
            var entities = _chunksNeedApplyVoxelChanges.GetEntityArray();
            var neighb = _chunksNeedApplyVoxelChanges.GetComponentDataArray<ChunkNeighboursComponent>();
            var j1 = new ApplyChangesVoxel
            {
                VoxelBuffers = _chunksNeedApplyVoxelChanges.GetBufferArray<Voxel>(),
                VoxelSetQuery = _chunksNeedApplyVoxelChanges.GetBufferArray<VoxelSetQueryData>(),
                ToBeRemeshedNeighbs = _toBeRemeshedCached.ToConcurrent(),
                Entities = entities,
                Neighbours = neighb,
            };
            var j2 = new ApplyChangesLight
            {
                Entities = entities,
                Neighbours = GetComponentDataFromEntity<ChunkNeighboursComponent>(true),
                LightBuffers = GetBufferFromEntity<VoxelLightingLevel>(),
                LightSetQuery = GetBufferFromEntity<LightSetQueryData>(),
                ToBeRemeshed = _toBeRemeshedCached,
                VoxelBuffers = GetBufferFromEntity<Voxel>(true),
                ToChangeLight = _toChangeLightCached,
                ToDepRegLight = _toDepRegLightCached,
                ToDepSunLight = _toDepSunLightCached,
                ToPropRegLight = _toPropRegLightCached,
                ToPropSunLight = _toPropSunLightCached,
            };
            var j3 = new ChangeTagsJob
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                AlreadyDirty = GetComponentDataFromEntity<ChunkDirtyComponent>(true),
                ToBeRemeshedNeighb = _toBeRemeshedCached,
                ToBeRemeshed = entities,
            };

            return j3.Schedule(j2.Schedule(j1.Schedule(entities.Length, 1, inputDeps)));
        }
    }
}
