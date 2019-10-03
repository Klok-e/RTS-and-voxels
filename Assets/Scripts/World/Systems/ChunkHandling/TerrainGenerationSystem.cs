using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using World.Components;
using World.DynamicBuffers;
using World.Utils;

namespace World.Systems.ChunkHandling
{
    [UpdateBefore(typeof(ApplyVoxelChangesSystem))]
    public class TerrainGenerationSystem : JobComponentSystem
    {
        private EntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var t1 = new GenerateChunkTerrainJob();
            var h1 = t1.Schedule(this, inputDeps);

            var t2 = new ChangeTagsJob
            {
                commandBuffer = _barrier.CreateCommandBuffer().ToConcurrent()
            };
            var h2 = t2.Schedule(this, h1);

            _barrier.AddJobHandleForProducer(h2);

            return h2;
        }

        private class TerrainGenerationBarrier : EntityCommandBufferSystem
        {
        }

        [BurstCompile]
        [RequireComponentTag(typeof(ChunkNeedTerrainGeneration))]
        private struct
            GenerateChunkTerrainJob : IJobForEachWithEntity_EBBC<Voxel, VoxelLightingLevel, ChunkPosComponent>
        {
            public void Execute(Entity                            entity, int index,
                                DynamicBuffer<Voxel>              voxelsBuffer,
                                DynamicBuffer<VoxelLightingLevel> lightBuffer, ref ChunkPosComponent c2)
            {
                const int chunkSize = VoxConsts.ChunkSize;

                var offset = c2.Pos;

                for (int z = 0; z < chunkSize; z++)
                for (int x = 0; x < chunkSize; x++)
                {
                    float fx     = x / (chunkSize - 1f);
                    float fz     = z / (chunkSize - 1f);
                    float sample = Perlin(fx, fz, offset);

                    for (int y = 0; y < chunkSize; y++)
                    {
                        float fy = y / (chunkSize - 1f) + offset.y;
                        if (fy < sample)
                        {
                            if ((y + 1) / (chunkSize - 1f) + offset.y > sample)
                                voxelsBuffer.AtSet(x, y, z,
                                    new Voxel
                                    {
                                        type = VoxelType.Grass
                                    });
                            else
                                voxelsBuffer.AtSet(x, y, z,
                                    new Voxel
                                    {
                                        type = VoxelType.Dirt
                                    });
                            lightBuffer.AtSet(x, y, z, new VoxelLightingLevel(0, 0));
                        }
                        else
                        {
                            voxelsBuffer.AtSet(x, y, z,
                                new Voxel
                                {
                                    type = VoxelType.Empty
                                });
                            lightBuffer.AtSet(x, y, z, new VoxelLightingLevel(0, VoxelLightingLevel.MaxLight));
                        }
                    }
                }
            }

            private float Perlin(float fx, float fz, int3 offset)
            {
                return 0.5f * Mathf.PerlinNoise((fx + offset.x) * 1.2f, (fz + offset.z) * 1.2f);
            }
        }

        [RequireComponentTag(typeof(ChunkNeedTerrainGeneration))]
        private struct ChangeTagsJob : IJobForEachWithEntity_EBBC<Voxel, VoxelLightingLevel, ChunkPosComponent>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;

            public void Execute(Entity                            entity,
                                int                               index,
                                DynamicBuffer<Voxel>              b0,
                                DynamicBuffer<VoxelLightingLevel> b1,
                                ref ChunkPosComponent             c2)
            {
                commandBuffer.AddComponent(index, entity, new ChunkDirtyComponent());
                commandBuffer.RemoveComponent<ChunkNeedTerrainGeneration>(index, entity);
            }
        }
    }
}