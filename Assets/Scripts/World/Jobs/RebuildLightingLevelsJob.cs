using Assets.Scripts.Help;
using Scripts.Help;
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
    public struct PropagateLightJob : IJob
    {
        public NativeArray3D<VoxelLightingLevel> lightingLevels;

        [ReadOnly]
        public NativeArray3D<Voxel> voxels;

        private const int xMask = 0b111111 << 12;
        private const int yMask = 0b111111 << 6;
        private const int zMask = 0b111111;

        public void Execute()
        {
            //unused             x      y      z
            //xxxxxxxxxxxxxx||xxxxxx||xxxxxx||xxxxxx
            NativeQueue<int> toProcess = new NativeQueue<int>(Allocator.TempJob);

            for (int i = 0; i < lightingLevels.XMax * lightingLevels.YMax * lightingLevels.ZMax; i++)
            {
                if (lightingLevels[i].Level > 0)
                {
                    lightingLevels.At(i, out int x, out int y, out int z);
                    toProcess.Enqueue(z | (y << 6) | (x << 12));
                }
            }
            while (toProcess.Count > 0)
            {
                var pos = toProcess.Dequeue();
                int x = (pos & xMask) >> 12;
                int y = (pos & yMask) >> 6;
                int z = (pos & zMask);

                var lightLvl = lightingLevels[x, y, z];

                //check 6 sides of a voxel
                for (int i = 0; i < 6; i++)
                {
                    var dir = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                    var vec = dir.DirectionToVec();

                    var xDir = x + vec.x;
                    var yDir = y + vec.y;
                    var zDir = z + vec.z;

                    if (xDir >= lightingLevels.XMax || xDir < 0
                        ||
                        yDir >= lightingLevels.YMax || yDir < 0
                        ||
                        zDir >= lightingLevels.ZMax || zDir < 0)
                    {
                        continue;
                    }
                    if (lightingLevels[xDir, yDir, zDir].Level < (lightLvl.Level - 1))
                    {
                        lightingLevels[xDir, yDir, zDir] = new VoxelLightingLevel()
                        {
                            Level = (byte)(lightLvl.Level - 1),
                        };
                        if (lightLvl.Level - 1 > 0)
                        {
                            toProcess.Enqueue(zDir | (yDir << 6) | (xDir << 12));
                        }
                    }
                }
            }
            toProcess.Dispose();
        }
    }
}
