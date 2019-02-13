﻿using Unity.Entities;
using Unity.Mathematics;

namespace Scripts.World.DynamicBuffers
{
    [InternalBufferCapacity(0)]
    public struct VoxelSetQueryData : IBufferElementData
    {
        public int3 Pos;
        public VoxelType NewVoxelType;
    }
}
