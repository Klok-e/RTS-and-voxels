using ProceduralNoiseProject;
using Scripts.Help;
using Scripts.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World.Jobs
{
    public struct GenerateChunkTerrainJob : IJob
    {
        [WriteOnly]
        public NativeArray3D<Voxel> voxels;

        [ReadOnly]
        public Vector3Int offset;

        [ReadOnly]
        public int chunkSize;

        public void Execute()
        {
            var fractal = new FractalNoise(new PerlinNoise(1337, 2.0f, 1.3f), 2, 0.2f, 1.5f)
            {
                Offset = offset
            };

            for (int x = 0; x < chunkSize; x++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    for (int z = 0; z < chunkSize; z++)
                    {
                        float fx = x / (chunkSize - 1f);
                        float fz = z / (chunkSize - 1f);
                        float fy = y / (chunkSize - 1f);
                        var fill = fractal.Sample3D(fx, fy, fz);

                        voxels[x, y, z] = new Voxel()
                        {
                            type = (fill * chunkSize > y + (offset.y * chunkSize)) ? VoxelType.Solid : VoxelType.Air,
                        };
                    }
                }
            }
        }
    }
}
