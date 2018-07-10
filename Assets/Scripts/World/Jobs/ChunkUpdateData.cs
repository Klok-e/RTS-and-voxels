using Scripts.Help;
using Scripts.Help.DataContainers;
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
        public NativeArray3D<Voxel> boxThatContainsChunkAndAllNeighboursBordersVox;
        public NativeArray3D<VoxelLightingLevel> boxThatContainsChunkAndAllNeighboursBordersLight;

        public RegularChunk _chunk;

        public JobHandle _updateJob;

        public void CompleteChunkCleaning()
        {
            _updateJob.Complete();
            _chunk.ApplyMeshData();

            boxThatContainsChunkAndAllNeighboursBordersVox.Dispose();
            boxThatContainsChunkAndAllNeighboursBordersLight.Dispose();
        }
    }

    public struct ChunkRebuildingVisibleFacesData
    {
        public NativeArray3D<Voxel> boxThatContainsChunkAndAllNeighboursBorders;

        public RegularChunk _chunk;

        public JobHandle _updateJob;

        public void CompleteChunkVisibleFacesRebuilding()
        {
            _updateJob.Complete();

            boxThatContainsChunkAndAllNeighboursBorders.Dispose();
        }
    }
}
