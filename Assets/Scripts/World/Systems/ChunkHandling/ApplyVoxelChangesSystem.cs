using Scripts.Help;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Scripts.World.Systems.Regions;
using Scripts.World.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using dirFlags = Scripts.Help.DirectionsHelper.BlockDirectionFlag;

namespace Scripts.World.Systems
{
    [UpdateBefore(typeof(ChunkMeshSystem))]
    public class ApplyVoxelChangesSystem : JobComponentSystem
    {
        private EntityQuery _chunksNeedApplyVoxelChanges;

        private NativeQueue<Entity> _toBeRemeshedCached;

        private NativeQueue<int3> _toPropRegLightCached;
        private NativeQueue<int3> _toPropSunLightCached;
        private NativeQueue<int3> _toDepRegLightCached;
        private NativeQueue<int3> _toDepSunLightCached;
        private NativeQueue<Entity> _toChangeLightCached;

        private EntityCommandBufferSystem _barrier;

        private RegionLoadUnloadSystem _chunkCreationSystem;

        [BurstCompile]
        [RequireComponentTag(typeof(ChunkNeedApplyVoxelChanges))]
        private struct ApplyChangesVoxel : IJobForEach_BBC<Voxel, VoxelSetQueryData, ChunkPosComponent>
        {
            public NativeQueue<Entity>.Concurrent ToBeRemeshedNeighbs;

            [ReadOnly]
            public NativeHashMap<int3, Entity> PosToEntity;

            public void Execute(DynamicBuffer<Voxel> buffer, DynamicBuffer<VoxelSetQueryData> query, ref ChunkPosComponent pos)
            {
                for(int i = 0; i < query.Length; i++)
                {
                    var x = query[i];

                    buffer.AtSet(x.Pos.x, x.Pos.y, x.Pos.z, new Voxel
                    {
                        Type = x.NewVoxelType,
                    });

                    var dir = DirectionsHelper.AreCoordsAtBordersOfChunk(x.Pos);
                    if(dir != dirFlags.None)
                    {
                        if(PosToEntity.TryGetValue(pos.Pos + dir.ToInt3(), out var ent))
                            ToBeRemeshedNeighbs.Enqueue(ent);
                    }
                }
                query.Clear();
            }
        }

        // TODO: Fix this abomination (user of light does not need to know propagation type)
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
            public NativeArray<Entity> Entities;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkPosComponent> Positions;

            [ReadOnly]
            public NativeHashMap<int3, Entity> PosToChunk;

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

                    var currPos = Positions[ent];
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
                        else if(curr.Propagation == PropagationType.Depropagate)
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
                                //var nextEnt = Neighbours[ent][dir];
                                if(PosToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))//nextEnt != Entity.Null)
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
                                    }
                                    ToChangeLight.Enqueue(nextEnt);
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
                                    if(PosToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))
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
                                            LightBuffers[nextEnt].AtSet(nextBlock.x, nextBlock.y, nextBlock.z, new VoxelLightingLevel(lightAtPos.RegularLight - 1, nLight.Sunlight));
                                        }
                                        ToChangeLight.Enqueue(nextEnt);
                                    }
                                }
                            }
                        }
                    }

                    // Depropagate sun light
                    while(ToDepSunLight.Count > 0)
                    {
                        var depr = ToDepSunLight.Dequeue();

                        var lightAtPos = currLight.AtGet(depr.x, depr.y, depr.z);

                        currLight.AtSet(depr.x, depr.y, depr.z, new VoxelLightingLevel(lightAtPos.RegularLight, 0));

                        // add neighbours to toDepReg
                        for(int i = 0; i < 6; i++)
                        {
                            var dir = (dirFlags)(1 << i);
                            var vec = dir.ToInt3();

                            var nextBlock = depr + vec;

                            var dirWrp = DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y, ref nextBlock.z);
                            if(dirWrp == dirFlags.None)
                            {
                                int nxtVal;
                                if(dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                    nxtVal = lightAtPos.Sunlight; // if down and max then set below to this light
                                else
                                    nxtVal = lightAtPos.Sunlight - 1; // else propagate normally

                                if(currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).Sunlight <= nxtVal) // less than supposed to be
                                    ToDepSunLight.Enqueue(nextBlock);
                                else
                                    ToPropSunLight.Enqueue(nextBlock);
                            }
                            else // not in this chunk
                            {
                                if(PosToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))
                                {
                                    var nextLight = LightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if(nextLight.Sunlight > 0)
                                    {
                                        int nxtVal;
                                        if(dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                            nxtVal = lightAtPos.Sunlight; // if down and max then set below to this light
                                        else
                                            nxtVal = lightAtPos.Sunlight - 1; // else propagate normally

                                        PropagationType pr;
                                        if(nextLight.Sunlight <= nxtVal)
                                            pr = PropagationType.Depropagate; // depropagate
                                        else
                                            pr = PropagationType.Propagate; // propagate

                                        LightSetQuery[nextEnt].Add(new LightSetQueryData
                                        {
                                            LightType = SetLightType.Sunlight,
                                            Pos = nextBlock,
                                            Propagation = pr,
                                        });
                                    }
                                    ToChangeLight.Enqueue(nextEnt);
                                }
                            }
                        }
                    }

                    // Propagate sun light
                    while(ToPropSunLight.Count > 0)
                    {
                        var prop = ToPropSunLight.Dequeue();

                        var lightAtPos = currLight.AtGet(prop.x, prop.y, prop.z);

                        if(lightAtPos.Sunlight > 0)
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
                                    if(nLight.Sunlight < lightAtPos.Sunlight - 1 // less than current
                                        &&
                                        currVox.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).Type.IsEmpty()) // and empty
                                    {
                                        int nxtVal;
                                        if(dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                            nxtVal = lightAtPos.Sunlight; // if down and max then set below to this light
                                        else
                                            nxtVal = lightAtPos.Sunlight - 1; // else propagate normally
                                        ToPropSunLight.Enqueue(nextBlock);
                                        currLight.AtSet(nextBlock.x, nextBlock.y, nextBlock.z, new VoxelLightingLevel(nLight.RegularLight, nxtVal));
                                    }
                                }
                                else // not in this chunk
                                {
                                    if(PosToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))
                                    {
                                        var nLight = LightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                        if(nLight.Sunlight < lightAtPos.Sunlight - 1 // less than current
                                            &&
                                            VoxelBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z).Type.IsEmpty()) // and empty
                                        {
                                            LightSetQuery[nextEnt].Add(new LightSetQueryData
                                            {
                                                LightType = SetLightType.Sunlight,
                                                Pos = nextBlock,
                                                Propagation = PropagationType.Propagate,
                                            });

                                            int nxtVal;
                                            if(dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                                nxtVal = lightAtPos.Sunlight; // if down and max then set below to this light
                                            else
                                                nxtVal = lightAtPos.Sunlight - 1; // else propagate normally
                                            LightBuffers[nextEnt].AtSet(nextBlock.x, nextBlock.y, nextBlock.z, new VoxelLightingLevel(nLight.RegularLight, nxtVal));
                                        }
                                        ToChangeLight.Enqueue(nextEnt);
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

            [DeallocateOnJobCompletion]
            public NativeArray<Entity> ToBeRemeshed;

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
            _chunksNeedApplyVoxelChanges = GetEntityQuery(
                ComponentType.ReadWrite<ChunkNeedApplyVoxelChanges>(),
                ComponentType.ReadWrite<Voxel>(),
                ComponentType.ReadWrite<VoxelSetQueryData>(),
                ComponentType.ReadWrite<LightSetQueryData>());

            _toBeRemeshedCached = new NativeQueue<Entity>(Allocator.Persistent);
            _toPropRegLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toPropSunLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toDepRegLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toDepSunLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toChangeLightCached = new NativeQueue<Entity>(Allocator.Persistent);

            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

            _chunkCreationSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
        }

        protected override void OnDestroyManager()
        {
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

            var j1 = new ApplyChangesVoxel
            {
                ToBeRemeshedNeighbs = _toBeRemeshedCached.ToConcurrent(),
                PosToEntity = _chunkCreationSystem.PosToChunkEntity,
            };

            var h1 = j1.Schedule(this, inputDeps);

            // TODO: fix this sheet (doesn't work without .Complete())
            h1.Complete();

            var ents = _chunksNeedApplyVoxelChanges.ToEntityArray(Allocator.TempJob, out var collectEntities);
            var j2 = new ApplyChangesLight
            {
                Entities = ents,
                LightBuffers = GetBufferFromEntity<VoxelLightingLevel>(),
                LightSetQuery = GetBufferFromEntity<LightSetQueryData>(),
                ToBeRemeshed = _toBeRemeshedCached,
                VoxelBuffers = GetBufferFromEntity<Voxel>(),
                ToChangeLight = _toChangeLightCached,
                ToDepRegLight = _toDepRegLightCached,
                ToDepSunLight = _toDepSunLightCached,
                ToPropRegLight = _toPropRegLightCached,
                ToPropSunLight = _toPropSunLightCached,
                Positions = GetComponentDataFromEntity<ChunkPosComponent>(),
                PosToChunk = _chunkCreationSystem.PosToChunkEntity,
            };
            var h2 = j2.Schedule(JobHandle.CombineDependencies(collectEntities, h1));

            var j3 = new ChangeTagsJob
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                AlreadyDirty = GetComponentDataFromEntity<ChunkDirtyComponent>(),
                ToBeRemeshedNeighb = _toBeRemeshedCached,
                ToBeRemeshed = ents,
            };
            var h3 = j3.Schedule(h2);

            _barrier.AddJobHandleForProducer(h3);

            return h3;
        }
    }
}
