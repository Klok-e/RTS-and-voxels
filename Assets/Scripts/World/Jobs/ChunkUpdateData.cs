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
    public struct ChunkCleaningData
    {
        public NativeArray3D<Voxel> _voxels;

        public NativeArray3D<VoxelLightingLevel> _lightingLevels,
            _lightingLevelsUp, _lightingLevelsDown, _lightingLevelsLeft, _lightingLevelsRight, _lightingLevelsBack, _lightingLevelsFront;

        public RegularChunk _chunk;

        public JobHandle _updateJob;

        public void CompleteChunkCleaning()
        {
            _updateJob.Complete();
            _chunk.ApplyMeshData();

            _voxels.Dispose();
            _lightingLevels.Dispose();
            _lightingLevelsUp.Dispose();
            _lightingLevelsDown.Dispose();
            _lightingLevelsBack.Dispose();
            _lightingLevelsFront.Dispose();
            _lightingLevelsLeft.Dispose();
            _lightingLevelsRight.Dispose();
        }
    }

    public struct ChunkRebuildingVisibleFacesData
    {
        public NativeArray3D<Voxel> _voxels,
        _voxelsUp, _voxelsDown, _voxelsLeft, _voxelsRight, _voxelsBack, _voxelsFront;

        public RegularChunk _chunk;

        public JobHandle _updateJob;

        public void CompleteChunkVisibleFacesRebuilding()
        {
            _updateJob.Complete();

            _voxels.Dispose();
            _voxelsBack.Dispose();
            _voxelsDown.Dispose();
            _voxelsFront.Dispose();
            _voxelsLeft.Dispose();
            _voxelsRight.Dispose();
            _voxelsUp.Dispose();
        }
    }
}
