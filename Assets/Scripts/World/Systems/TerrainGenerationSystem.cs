using Scripts.World.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    public class TerrainGenerationSystem : JobComponentSystem
    {
        private ComponentGroup _needGeneration;

        [UpdateAfter(typeof(TerrainGenerationSystem))]
        private class TerrainGenerationBarrier : BarrierSystem { }
        [Inject]
        private TerrainGenerationBarrier _barrier;

        [BurstCompile]
        public struct GenerateChunkTerrainJob : IJobProcessComponentDataWithEntity<ChunkPosComponent>
        {
            [WriteOnly]
            public BufferArray<Voxel> voxelBuffers;
            [WriteOnly]
            public BufferArray<VoxelLightingLevel> lightBuffers;

            public void Execute(Entity entity, int index, [ReadOnly] ref ChunkPosComponent c0)
            {
                const int chunkSize = VoxConsts._chunkSize;

                var offset = c0.Pos;
                var voxelsBuffer = voxelBuffers[index];
                var lightBuffer = lightBuffers[index];
                for(int z = 0; z < chunkSize; z++)
                {
                    for(int x = 0; x < chunkSize; x++)
                    {
                        float fx = x / (chunkSize - 1f);
                        float fz = z / (chunkSize - 1f);
                        float sample = Perlin(fx, fz, offset);

                        for(int y = 0; y < chunkSize; y++)
                        {
                            float fy = y / (chunkSize - 1f) + offset.y;
                            if(fy < sample)
                            {
                                if(((y + 1) / (chunkSize - 1f) + offset.y) > sample)
                                    voxelsBuffer.AtSet(x, y, z,
                                        new Voxel()
                                        {
                                            Type = VoxelType.Grass,
                                        });
                                else
                                    voxelsBuffer.AtSet(x, y, z,
                                        new Voxel()
                                        {
                                            Type = VoxelType.Dirt,
                                        });
                            }
                            else
                            {
                                voxelsBuffer.AtSet(x, y, z,
                                    new Voxel()
                                    {
                                        Type = VoxelType.Empty,
                                    });
                                lightBuffer.AtSet(x, y, z, new VoxelLightingLevel(0, VoxelLightingLevel.maxLight));
                            }
                        }

                    }
                }
            }

            private float Perlin(float fx, float fz, int3 offset)
            {
                return 0.5f * Mathf.PerlinNoise((fx + offset.x) * 1.2f, (fz + offset.z) * 1.2f);
            }
        }

        public struct ChangeTagsJob : IJobProcessComponentDataWithEntity<ChunkNeedTerrainGeneration>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;

            public void Execute(Entity entity, int index, ref ChunkNeedTerrainGeneration c0)
            {
                commandBuffer.AddComponent(index, entity, new ChunkDirtyComponent());
                commandBuffer.RemoveComponent<ChunkNeedTerrainGeneration>(index, entity);
            }
        }

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _needGeneration = EntityManager.CreateComponentGroup(
                ComponentType.Create<ChunkNeedTerrainGeneration>(),
                ComponentType.Create<VoxelLightingLevel>(),
                ComponentType.Create<Voxel>());
            RequireForUpdate(_needGeneration);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var t1 = new GenerateChunkTerrainJob
            {
                lightBuffers = _needGeneration.GetBufferArray<VoxelLightingLevel>(),
                voxelBuffers = _needGeneration.GetBufferArray<Voxel>(),

            };
            var t2 = new ChangeTagsJob()
            {
                commandBuffer = _barrier.CreateCommandBuffer().ToConcurrent(),
            };
            return t2.Schedule(this, t1.Schedule(this, inputDeps));
        }
    }
}
