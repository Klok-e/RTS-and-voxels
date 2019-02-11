using Scripts.World.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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

        //[BurstCompile]
        public struct GenerateChunkTerrainJob : IJob
        {
            [WriteOnly]
            public BufferArray<Voxel> voxelBuffers;
            [WriteOnly]
            public BufferArray<VoxelLightingLevel> lightBuffers;
            [ReadOnly]
            public ComponentArray<RegularChunk> chunks;
            public EntityArray entities;

            public EntityCommandBuffer commandBuffer;

            public void Execute()
            {
                const int chunkSize = VoxelWorld._chunkSize;
                for(int i = 0; i < chunks.Length; i++)
                {
                    commandBuffer.AddComponent(entities[i], new ChunkDirtyComponent());
                    commandBuffer.RemoveComponent<ChunkNeedTerrainGeneration>(entities[i]);

                    var offset = chunks[i].Pos;
                    var voxelsBuffer = voxelBuffers[i];
                    var lightBuffer = lightBuffers[i];
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
            }

            private float Perlin(float fx, float fz, Vector3Int offset)
            {
                return 0.5f * Mathf.PerlinNoise((fx + offset.x) * 1.2f, (fz + offset.z) * 1.2f);
            }
        }

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _needGeneration = EntityManager.CreateComponentGroup(typeof(ChunkNeedTerrainGeneration), typeof(RegularChunk), typeof(VoxelLightingLevel), typeof(Voxel));
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            new GenerateChunkTerrainJob
            {
                chunks = _needGeneration.GetComponentArray<RegularChunk>(),
                lightBuffers = _needGeneration.GetBufferArray<VoxelLightingLevel>(),
                voxelBuffers = _needGeneration.GetBufferArray<Voxel>(),
                entities = _needGeneration.GetEntityArray(),
                commandBuffer = _barrier.CreateCommandBuffer(),
            }.Schedule(inputDeps).Complete();
            return default;
        }
    }
}
