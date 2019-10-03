using Help;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using World.Components;
using World.DynamicBuffers;
using World.Systems.Regions;
using World.Utils;
using dirFlags = Help.DirectionsHelper.BlockDirectionFlag;

namespace World.Systems.ChunkHandling
{
    [UpdateBefore(typeof(ChunkMeshSystem))]
    public class ApplyVoxelChangesSystem : JobComponentSystem
    {
        private EntityCommandBufferSystem _barrier;

        private RegionLoadUnloadSystem _chunkCreationSystem;
        private EntityQuery            _chunksNeedApplyVoxelChanges;

        private NativeQueue<Entity> _toBeRemeshedCached;
        private NativeQueue<Entity> _toChangeLightCached;
        private NativeQueue<int3>   _toDepRegLightCached;
        private NativeQueue<int3>   _toDepSunLightCached;

        private NativeQueue<int3> _toPropRegLightCached;
        private NativeQueue<int3> _toPropSunLightCached;

        protected override void OnCreate()
        {
            _chunksNeedApplyVoxelChanges = GetEntityQuery(
                ComponentType.ReadWrite<ChunkNeedApplyVoxelChanges>(),
                ComponentType.ReadWrite<Voxel>(),
                ComponentType.ReadWrite<VoxelSetQueryData>(),
                ComponentType.ReadWrite<LightSetQueryData>());

            _toBeRemeshedCached   = new NativeQueue<Entity>(Allocator.Persistent);
            _toPropRegLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toPropSunLightCached = new NativeQueue<int3>(Allocator.Persistent);
            _toDepRegLightCached  = new NativeQueue<int3>(Allocator.Persistent);
            _toDepSunLightCached  = new NativeQueue<int3>(Allocator.Persistent);
            _toChangeLightCached  = new NativeQueue<Entity>(Allocator.Persistent);

            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

            _chunkCreationSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
        }

        protected override void OnDestroy()
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
                toBeRemeshedNeighbs = _toBeRemeshedCached.ToConcurrent(),
                posToEntity         = _chunkCreationSystem.PosToChunkEntity
            };

            var h1 = j1.Schedule(this, inputDeps);

            // TODO: fix this sheet (doesn't work without .Complete())
            h1.Complete();

            var ents = _chunksNeedApplyVoxelChanges.ToEntityArray(Allocator.TempJob, out var collectEntities);
            var j2 = new ApplyChangesLight
            {
                entities       = ents,
                lightBuffers   = GetBufferFromEntity<VoxelLightingLevel>(),
                lightSetQuery  = GetBufferFromEntity<LightSetQueryData>(),
                toBeRemeshed   = _toBeRemeshedCached,
                voxelBuffers   = GetBufferFromEntity<Voxel>(),
                toChangeLight  = _toChangeLightCached,
                toDepRegLight  = _toDepRegLightCached,
                toDepSunLight  = _toDepSunLightCached,
                toPropRegLight = _toPropRegLightCached,
                toPropSunLight = _toPropSunLightCached,
                positions      = GetComponentDataFromEntity<ChunkPosComponent>(),
                posToChunk     = _chunkCreationSystem.PosToChunkEntity
            };
            var h2 = j2.Schedule(JobHandle.CombineDependencies(collectEntities, h1));

            var j3 = new ChangeTagsJob
            {
                commandBuffer      = _barrier.CreateCommandBuffer(),
                alreadyDirty       = GetComponentDataFromEntity<ChunkDirtyComponent>(),
                toBeRemeshedNeighb = _toBeRemeshedCached,
                toBeRemeshed       = ents
            };
            var h3 = j3.Schedule(h2);

            _barrier.AddJobHandleForProducer(h3);

            return h3;
        }

        [BurstCompile]
        [RequireComponentTag(typeof(ChunkNeedApplyVoxelChanges))]
        private struct ApplyChangesVoxel : IJobForEach_BBC<Voxel, VoxelSetQueryData, ChunkPosComponent>
        {
            public NativeQueue<Entity>.Concurrent toBeRemeshedNeighbs;

            [ReadOnly]
            public NativeHashMap<int3, Entity> posToEntity;

            public void Execute(DynamicBuffer<Voxel>  buffer, DynamicBuffer<VoxelSetQueryData> query,
                                ref ChunkPosComponent pos)
            {
                for (int i = 0; i < query.Length; i++)
                {
                    var x = query[i];

                    buffer.AtSet(x.Pos.x, x.Pos.y, x.Pos.z, new Voxel
                    {
                        type = x.NewVoxelType
                    });

                    var dir = DirectionsHelper.AreCoordsAtBordersOfChunk(x.Pos);
                    if (dir != dirFlags.None)
                        if (posToEntity.TryGetValue(pos.Pos + dir.ToInt3(), out var ent))
                            toBeRemeshedNeighbs.Enqueue(ent);
                }

                query.Clear();
            }
        }

        // TODO: Fix this abomination (user of light does not need to know propagation type)
        [BurstCompile]
        private struct ApplyChangesLight : IJob
        {
            public NativeQueue<int3>   toPropRegLight;
            public NativeQueue<int3>   toPropSunLight;
            public NativeQueue<int3>   toDepRegLight;
            public NativeQueue<int3>   toDepSunLight;
            public NativeQueue<Entity> toChangeLight;

            public NativeQueue<Entity> toBeRemeshed;

            [ReadOnly]
            public BufferFromEntity<Voxel> voxelBuffers;

            public BufferFromEntity<VoxelLightingLevel> lightBuffers;

            public BufferFromEntity<LightSetQueryData> lightSetQuery;

            [ReadOnly]
            public NativeArray<Entity> entities;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkPosComponent> positions;

            [ReadOnly]
            public NativeHashMap<int3, Entity> posToChunk;

            public void Execute()
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var lightSet = lightSetQuery[entities[i]];
                    if (lightSet.Length > 0)
                        toChangeLight.Enqueue(entities[i]);
                }

                while (toChangeLight.Count > 0)
                {
                    var ent = toChangeLight.Dequeue();
                    toBeRemeshed.Enqueue(ent);

                    var currPos   = positions[ent];
                    var currLight = lightBuffers[ent];
                    var currSet   = lightSetQuery[ent];
                    var currVox   = voxelBuffers[ent];

                    for (int i = 0; i < currSet.Length; i++)
                    {
                        var curr = currSet[i];

                        var  p       = curr.pos;
                        var  atLight = currLight.AtGet(p.x, p.y, p.z);
                        byte newLvl  = curr.newLight;
                        if (curr.propagation == PropagationType.Regular)
                        {
                            if (curr.lightType == SetLightType.RegularLight)
                            {
                                if (atLight.RegularLight < newLvl)
                                {
                                    toPropRegLight.Enqueue(curr.pos);
                                    currLight.AtSet(p.x, p.y, p.z, new VoxelLightingLevel(newLvl, atLight.Sunlight));
                                }
                                else if (atLight.RegularLight >= newLvl)
                                {
                                    toDepRegLight.Enqueue(curr.pos);
                                }
                            }
                            else
                            {
                                if (atLight.Sunlight < newLvl)
                                {
                                    toPropSunLight.Enqueue(curr.pos);
                                    currLight.AtSet(p.x, p.y, p.z,
                                        new VoxelLightingLevel(atLight.RegularLight, newLvl));
                                }
                                else if (atLight.Sunlight >= newLvl)
                                {
                                    toDepSunLight.Enqueue(curr.pos);
                                }
                            }
                        }
                        else if (curr.propagation == PropagationType.Depropagate)
                        {
                            if (curr.lightType == SetLightType.RegularLight)
                                toDepRegLight.Enqueue(curr.pos);
                            else
                                toDepSunLight.Enqueue(curr.pos);
                        }
                        else
                        {
                            if (curr.lightType == SetLightType.RegularLight)
                                toPropRegLight.Enqueue(curr.pos);
                            else
                                toPropSunLight.Enqueue(curr.pos);
                        }
                    }

                    currSet.Clear();

                    // Depropagate regular light
                    while (toDepRegLight.Count > 0)
                    {
                        var depr = toDepRegLight.Dequeue();

                        var lightAtPos = currLight.AtGet(depr.x, depr.y, depr.z);

                        currLight.AtSet(depr.x, depr.y, depr.z, new VoxelLightingLevel(0, lightAtPos.Sunlight));

                        // add neighbours to toDepReg
                        for (int i = 0; i < 6; i++)
                        {
                            var dir = (dirFlags) (1 << i);
                            var vec = dir.ToInt3();

                            var nextBlock = depr + vec;

                            var dirWrp =
                                DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y, ref nextBlock.z);
                            if (dirWrp == dirFlags.None)
                            {
                                if (currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).RegularLight <
                                    lightAtPos.RegularLight) // less than supposed to be
                                    toDepRegLight.Enqueue(nextBlock);
                                else
                                    toPropRegLight.Enqueue(nextBlock);
                            }
                            else // not in this chunk
                            {
                                //var nextEnt = Neighbours[ent][dir];
                                if (posToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt)
                                ) //nextEnt != Entity.Null)
                                {
                                    var nextLight = lightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if (nextLight.RegularLight > 0)
                                    {
                                        PropagationType pr;
                                        if (nextLight.RegularLight < lightAtPos.RegularLight)
                                            pr = PropagationType.Depropagate; // depropagate
                                        else
                                            pr = PropagationType.Propagate; // propagate

                                        lightSetQuery[nextEnt].Add(new LightSetQueryData
                                        {
                                            lightType   = SetLightType.RegularLight,
                                            pos         = nextBlock,
                                            propagation = pr
                                        });
                                    }

                                    toChangeLight.Enqueue(nextEnt);
                                }
                            }
                        }
                    }

                    // Propagate regular light
                    while (toPropRegLight.Count > 0)
                    {
                        var prop = toPropRegLight.Dequeue();

                        var lightAtPos = currLight.AtGet(prop.x, prop.y, prop.z);

                        if (lightAtPos.RegularLight > 0)
                            // propagate to neighbours
                            for (int i = 0; i < 6; i++)
                            {
                                var dir = (dirFlags) (1 << i);
                                var vec = dir.ToInt3();

                                var nextBlock = prop + vec;

                                var dirWrp = DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y,
                                    ref nextBlock.z);
                                if (dirWrp == dirFlags.None)
                                {
                                    var nLight = currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if (nLight.RegularLight < lightAtPos.RegularLight - 1 // less than current
                                        &&
                                        currVox.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).type
                                               .IsEmpty()) // and empty
                                    {
                                        toPropRegLight.Enqueue(nextBlock);
                                        currLight.AtSet(nextBlock.x, nextBlock.y, nextBlock.z,
                                            new VoxelLightingLevel(lightAtPos.RegularLight - 1, nLight.Sunlight));
                                    }
                                }
                                else // not in this chunk
                                {
                                    if (posToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))
                                    {
                                        var nLight = lightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                        if (nLight.RegularLight < lightAtPos.RegularLight - 1 // less than current
                                            &&
                                            voxelBuffers[nextEnt]
                                                .AtGet(nextBlock.x, nextBlock.y, nextBlock.z).type
                                                .IsEmpty()) // and empty
                                        {
                                            lightSetQuery[nextEnt].Add(new LightSetQueryData
                                            {
                                                lightType   = SetLightType.RegularLight,
                                                pos         = nextBlock,
                                                propagation = PropagationType.Propagate
                                            });
                                            lightBuffers[nextEnt].AtSet(nextBlock.x, nextBlock.y, nextBlock.z,
                                                new VoxelLightingLevel(lightAtPos.RegularLight - 1, nLight.Sunlight));
                                        }

                                        toChangeLight.Enqueue(nextEnt);
                                    }
                                }
                            }
                    }

                    // Depropagate sun light
                    while (toDepSunLight.Count > 0)
                    {
                        var depr = toDepSunLight.Dequeue();

                        var lightAtPos = currLight.AtGet(depr.x, depr.y, depr.z);

                        currLight.AtSet(depr.x, depr.y, depr.z, new VoxelLightingLevel(lightAtPos.RegularLight, 0));

                        // add neighbours to toDepReg
                        for (int i = 0; i < 6; i++)
                        {
                            var dir = (dirFlags) (1 << i);
                            var vec = dir.ToInt3();

                            var nextBlock = depr + vec;

                            var dirWrp =
                                DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y, ref nextBlock.z);
                            if (dirWrp == dirFlags.None)
                            {
                                int nxtVal;
                                if (dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                    nxtVal = lightAtPos.Sunlight; // if down and max then set below to this light
                                else
                                    nxtVal = lightAtPos.Sunlight - 1; // else propagate normally

                                if (currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).Sunlight <= nxtVal
                                ) // less than supposed to be
                                    toDepSunLight.Enqueue(nextBlock);
                                else
                                    toPropSunLight.Enqueue(nextBlock);
                            }
                            else // not in this chunk
                            {
                                if (posToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))
                                {
                                    var nextLight = lightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if (nextLight.Sunlight > 0)
                                    {
                                        int nxtVal;
                                        if (dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                            nxtVal = lightAtPos
                                                .Sunlight; // if down and max then set below to this light
                                        else
                                            nxtVal = lightAtPos.Sunlight - 1; // else propagate normally

                                        PropagationType pr;
                                        if (nextLight.Sunlight <= nxtVal)
                                            pr = PropagationType.Depropagate; // depropagate
                                        else
                                            pr = PropagationType.Propagate; // propagate

                                        lightSetQuery[nextEnt].Add(new LightSetQueryData
                                        {
                                            lightType   = SetLightType.Sunlight,
                                            pos         = nextBlock,
                                            propagation = pr
                                        });
                                    }

                                    toChangeLight.Enqueue(nextEnt);
                                }
                            }
                        }
                    }

                    // Propagate sun light
                    while (toPropSunLight.Count > 0)
                    {
                        var prop = toPropSunLight.Dequeue();

                        var lightAtPos = currLight.AtGet(prop.x, prop.y, prop.z);

                        if (lightAtPos.Sunlight > 0)
                            // propagate to neighbours
                            for (int i = 0; i < 6; i++)
                            {
                                var dir = (dirFlags) (1 << i);
                                var vec = dir.ToInt3();

                                var nextBlock = prop + vec;

                                var dirWrp = DirectionsHelper.WrapCoordsInChunk(ref nextBlock.x, ref nextBlock.y,
                                    ref nextBlock.z);
                                if (dirWrp == dirFlags.None)
                                {
                                    var nLight = currLight.AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                    if (nLight.Sunlight < lightAtPos.Sunlight - 1 // less than current
                                        &&
                                        currVox.AtGet(nextBlock.x, nextBlock.y, nextBlock.z).type
                                               .IsEmpty()) // and empty
                                    {
                                        int nxtVal;
                                        if (dir == dirFlags.Down && lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                            nxtVal = lightAtPos
                                                .Sunlight; // if down and max then set below to this light
                                        else
                                            nxtVal = lightAtPos.Sunlight - 1; // else propagate normally
                                        toPropSunLight.Enqueue(nextBlock);
                                        currLight.AtSet(nextBlock.x, nextBlock.y, nextBlock.z,
                                            new VoxelLightingLevel(nLight.RegularLight, nxtVal));
                                    }
                                }
                                else // not in this chunk
                                {
                                    if (posToChunk.TryGetValue(currPos.Pos + dir.ToInt3(), out var nextEnt))
                                    {
                                        var nLight = lightBuffers[nextEnt].AtGet(nextBlock.x, nextBlock.y, nextBlock.z);
                                        if (nLight.Sunlight < lightAtPos.Sunlight - 1 // less than current
                                            &&
                                            voxelBuffers[nextEnt]
                                                .AtGet(nextBlock.x, nextBlock.y, nextBlock.z).type
                                                .IsEmpty()) // and empty
                                        {
                                            lightSetQuery[nextEnt].Add(new LightSetQueryData
                                            {
                                                lightType   = SetLightType.Sunlight,
                                                pos         = nextBlock,
                                                propagation = PropagationType.Propagate
                                            });

                                            int nxtVal;
                                            if (dir                 == dirFlags.Down &&
                                                lightAtPos.Sunlight == VoxelLightingLevel.MaxLight)
                                                nxtVal = lightAtPos
                                                    .Sunlight; // if down and max then set below to this light
                                            else
                                                nxtVal = lightAtPos.Sunlight - 1; // else propagate normally
                                            lightBuffers[nextEnt].AtSet(nextBlock.x, nextBlock.y, nextBlock.z,
                                                new VoxelLightingLevel(nLight.RegularLight, nxtVal));
                                        }

                                        toChangeLight.Enqueue(nextEnt);
                                    }
                                }
                            }
                    }
                }
            }
        }

        private struct ChangeTagsJob : IJob
        {
            public EntityCommandBuffer commandBuffer;

            [ReadOnly]
            public ComponentDataFromEntity<ChunkDirtyComponent> alreadyDirty;

            public NativeQueue<Entity> toBeRemeshedNeighb;

            [DeallocateOnJobCompletion]
            public NativeArray<Entity> toBeRemeshed;

            public void Execute()
            {
                // used as a hash map
                var processedEntities =
                    new NativeHashMap<Entity, int>(toBeRemeshedNeighb.Count + toBeRemeshed.Length, Allocator.Temp);

                for (int i = 0; i < toBeRemeshed.Length; i++)
                {
                    commandBuffer.RemoveComponent<ChunkNeedApplyVoxelChanges>(toBeRemeshed[i]);
                    if (!alreadyDirty.Exists(toBeRemeshed[i]))
                        commandBuffer.AddComponent(toBeRemeshed[i], new ChunkDirtyComponent());
                    processedEntities.TryAdd(toBeRemeshed[i], 0);
                }

                while (toBeRemeshedNeighb.TryDequeue(out var ent))
                    if (!processedEntities.TryGetValue(ent, out int _))
                    {
                        if (!alreadyDirty.Exists(ent))
                            commandBuffer.AddComponent(ent, new ChunkDirtyComponent());
                        processedEntities.TryAdd(ent, 0);
                    }

                processedEntities.Dispose();
            }
        }
    }
}