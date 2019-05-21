using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;

namespace Scripts.World.Components
{
    public struct RegionPosComponent : IComponentData
    {
        public int3 Pos;
    }

    public struct RegionNeedLoadComponentTag : IComponentData
    {
    }

    public struct RegionChunks : IBufferElementData
    {
        public Entity Chunk;
    }
}
