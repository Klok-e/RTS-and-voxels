using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems
{
    [UpdateBefore(typeof(ApplyVoxelChangesSystem))]
    public class TerrainGenerationSystem : JobComponentSystem
    {
        private ComponentGroup _needGeneration;

        private class TerrainGenerationBarrier : BarrierSystem { }
        [Inject]
        private EndFrameBarrier _barrier;

        [BurstCompile]
        public struct GenerateChunkTerrainJob : IJobParallelFor
        {
            [WriteOnly]
            public BufferArray<Voxel> VoxelBuffers;
            [WriteOnly]
            public BufferArray<VoxelLightingLevel> LightBuffers;
            [ReadOnly]
            public ComponentDataArray<ChunkPosComponent> Positions;

            public void Execute(int index)
            {
                const int chunkSize = VoxConsts._chunkSize;

                var offset = Positions[index].Pos;
                var voxelsBuffer = VoxelBuffers[index];
                var lightBuffer = LightBuffers[index];
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
                                lightBuffer.AtSet(x, y, z, new VoxelLightingLevel(0, 0));
                            }
                            else
                            {
                                voxelsBuffer.AtSet(x, y, z,
                                    new Voxel()
                                    {
                                        Type = VoxelType.Empty,
                                    });
                                lightBuffer.AtSet(x, y, z, new VoxelLightingLevel(0, VoxelLightingLevel.MaxLight));
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

        public struct ChangeTagsJob : IJob
        {
            public EntityCommandBuffer CommandBuffer;
            public EntityArray Entities;

            public void Execute()
            {
                for(int i = 0; i < Entities.Length; i++)
                {
                    CommandBuffer.AddComponent(Entities[i], new ChunkDirtyComponent());
                    CommandBuffer.RemoveComponent<ChunkNeedTerrainGeneration>(Entities[i]);
                }
            }
        }

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _needGeneration = GetComponentGroup(
                ComponentType.Create<ChunkNeedTerrainGeneration>(),
                ComponentType.Create<VoxelLightingLevel>(),
                ComponentType.Create<Voxel>(),
                ComponentType.ReadOnly<ChunkPosComponent>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var t1 = new GenerateChunkTerrainJob
            {
                LightBuffers = _needGeneration.GetBufferArray<VoxelLightingLevel>(),
                VoxelBuffers = _needGeneration.GetBufferArray<Voxel>(),
                Positions = _needGeneration.GetComponentDataArray<ChunkPosComponent>(),
            };
            var t2 = new ChangeTagsJob()
            {
                CommandBuffer = _barrier.CreateCommandBuffer(),
                Entities = _needGeneration.GetEntityArray(),
            };
            return JobHandle.CombineDependencies(
                t2.Schedule(inputDeps),
                t1.Schedule(t2.Entities.Length, 1, inputDeps));
        }
    }
}
