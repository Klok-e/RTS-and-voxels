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
    public struct PropagateLightJobData
    {
        public NativeArray3D<Voxel> voxels,
            voxelsUp, voxelsDown, voxelsLeft, voxelsRight, voxelsBack, voxelsFront;

        public JobHandle jobHandle;

        public NativeArray<DirectionsHelper.BlockDirectionFlag> chunksAffected;
    }

    public struct PropagateLightJob : IJob
    {
        public NativeArray3D<VoxelLightingLevel> lightingLevels,
            lightingLevelsUp, lightingLevelsDown, lightingLevelsLeft, lightingLevelsRight, lightingLevelsBack, lightingLevelsFront;

        /// <summary>
        /// Initialized in the job. 1 element NativeArray to show what adjacent chunks have been affected.
        /// </summary>
        public NativeArray<DirectionsHelper.BlockDirectionFlag> chunksAffected;

        [ReadOnly]
        public int chunkSize;

        [ReadOnly]
        public NativeArray3D<Voxel> voxels,
            voxelsUp, voxelsDown, voxelsLeft, voxelsRight, voxelsBack, voxelsFront;

        private const int xMask = 0b111111 << 12;
        private const int yMask = 0b111111 << 6;
        private const int zMask = 0b111111;

        public void Execute()
        {
            //unused             x      y      z
            //xxxxxxxxxxxxxx||xxxxxx||xxxxxx||xxxxxx
            NativeQueue<int> toProcess = new NativeQueue<int>(Allocator.TempJob);

            for (int i = 0; i < chunkSize * chunkSize * chunkSize; i++)
            {
                if (lightingLevels[i]._level > 0)
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

                    if (xDir >= chunkSize || xDir < 0
                        ||
                        yDir >= chunkSize || yDir < 0
                        ||
                        zDir >= chunkSize || zDir < 0)
                    {
                        int dirIndX = xDir,
                            dirIndY = yDir,
                            dirIndZ = zDir;

                        if (xDir >= chunkSize) dirIndX = 0;
                        else if (xDir < 0) dirIndX = chunkSize - 1;

                        if (yDir >= chunkSize) dirIndY = 0;
                        else if (yDir < 0) dirIndY = chunkSize - 1;

                        if (zDir >= chunkSize) dirIndZ = 0;
                        else if (zDir < 0) dirIndZ = chunkSize - 1;

                        NativeArray3D<Voxel> voxelsDir;
                        switch (dir)
                        {
                            case DirectionsHelper.BlockDirectionFlag.Up: voxelsDir = voxelsUp; break;
                            case DirectionsHelper.BlockDirectionFlag.Down: voxelsDir = voxelsDown; break;
                            case DirectionsHelper.BlockDirectionFlag.Left: voxelsDir = voxelsLeft; break;
                            case DirectionsHelper.BlockDirectionFlag.Right: voxelsDir = voxelsRight; break;
                            case DirectionsHelper.BlockDirectionFlag.Back: voxelsDir = voxelsBack; break;
                            case DirectionsHelper.BlockDirectionFlag.Front: voxelsDir = voxelsFront; break;
                            default: throw new Exception();
                        }

                        NativeArray3D<VoxelLightingLevel> lightLvlDir;
                        switch (dir)
                        {
                            case DirectionsHelper.BlockDirectionFlag.Up: lightLvlDir = lightingLevelsUp; break;
                            case DirectionsHelper.BlockDirectionFlag.Down: lightLvlDir = lightingLevelsDown; break;
                            case DirectionsHelper.BlockDirectionFlag.Left: lightLvlDir = lightingLevelsLeft; break;
                            case DirectionsHelper.BlockDirectionFlag.Right: lightLvlDir = lightingLevelsRight; break;
                            case DirectionsHelper.BlockDirectionFlag.Back: lightLvlDir = lightingLevelsBack; break;
                            case DirectionsHelper.BlockDirectionFlag.Front: lightLvlDir = lightingLevelsFront; break;
                            default: throw new Exception();
                        }

                        if (lightLvlDir[dirIndX, dirIndY, dirIndZ]._level < (lightLvl._level - 1))
                        {
                            lightLvlDir[dirIndX, dirIndY, dirIndZ] = new VoxelLightingLevel()
                            {
                                _level = (byte)(lightLvl._level - 1),
                            };
                            if (lightLvl._level - 1 > 0)
                            {
                                chunksAffected[0] |= dir;
                            }
                        }
                    }
                    else if (lightingLevels[xDir, yDir, zDir]._level < (lightLvl._level - 1))
                    {
                        lightingLevels[xDir, yDir, zDir] = new VoxelLightingLevel()
                        {
                            _level = (byte)(lightLvl._level - 1),
                        };
                        if (lightLvl._level - 1 > 0)
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
