using Scripts.Help.DataContainers;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World.Jobs
{
    [BurstCompile]
    public struct GenerateChunkTerrainJob : IJob
    {
        [WriteOnly]
        public NativeArray3D<Voxel> voxels;

        [WriteOnly]
        public NativeArray3D<VoxelLightingLevel> light;

        [ReadOnly]
        public Vector3Int offset;

        [ReadOnly]
        public int chunkSize;

        public void Execute()
        {
            for(int z = 0; z < chunkSize; z++)
            {
                for(int x = 0; x < chunkSize; x++)
                {
                    float fx = x / (chunkSize - 1f);
                    float fz = z / (chunkSize - 1f);
                    float sample = Perlin(fx, fz);

                    for(int y = 0; y < chunkSize; y++)
                    {
                        float fy = y / (chunkSize - 1f) + offset.y;
                        if(fy < sample)
                        {
                            if(((y + 1) / (chunkSize - 1f) + offset.y) > sample)
                                voxels[x, y, z] = new Voxel()
                                {
                                    type = VoxelType.Grass,
                                };
                            else
                                voxels[x, y, z] = new Voxel()
                                {
                                    type = VoxelType.Dirt,
                                };
                        }
                        else
                        {
                            voxels[x, y, z] = new Voxel()
                            {
                                type = VoxelType.Empty,
                            };
                            light[x, y, z] = new VoxelLightingLevel(0, VoxelLightingLevel.maxLight);
                        }
                    }
                }
            }
        }

        private float Perlin(float fx, float fz)
        {
            return 0.5f * Mathf.PerlinNoise((fx + offset.x) * 1.2f, (fz + offset.z) * 1.2f);
        }
    }
}
