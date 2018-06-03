using Scripts.Help;
using Scripts.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;

namespace Scripts.World.Jobs
{
    public struct ChunkUpdateData
    {
        public NativeArray3D<Voxel> _voxels,
        _voxelsUp, _voxelsDown, _voxelsLeft, _voxelsRight, _voxelsBack, _voxelsFront;

        public RegularChunk _chunk;

        public NativeArray3D<VoxelLightingLevel> _lightingLevels;

        public JobHandle _updateJob;
    }
}
