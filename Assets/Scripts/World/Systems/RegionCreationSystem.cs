using Scripts.World.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Scripts.World.Systems
{
    public class RegionCreationSystem : ComponentSystem
    {
        public NativeHashMap<int3, Entity> Regions { get; private set; }
        private int3 _loaderChunkInPrev;

        protected override void OnCreate()
        {
            Regions = new NativeHashMap<int3, Entity>(100, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            Regions.Dispose();
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((ref MapLoader loader, ref LocalToWorld pos) =>
            {
                var loaderChunkIn = ChunkIn(pos.Position);
                if(math.any(_loaderChunkInPrev != loaderChunkIn))
                {
                    _loaderChunkInPrev = loaderChunkIn;

                    // gen new
                    for(int x = -loader.RegionDistance; x <= loader.RegionDistance; x++)
                        for(int y = -loader.RegionDistance / 2; y <= loader.RegionDistance / 2; y++)
                            for(int z = -loader.RegionDistance; z <= loader.RegionDistance; z++)
                            {
                                var chPos = new int3(x, y, z) + loaderChunkIn;
                                if(!Regions.TryGetValue(chPos, out var _))
                                    CreateChunk(chPos);
                            }

                    // prune old
                    using(var keys = Regions.GetKeyArray(Allocator.Temp))
                        foreach(var key in keys)
                        {
                            if(math.distance(loaderChunkIn, key) > loader.RegionDistance)
                                RemoveChunk(key);
                        }
                }
            });
        }
    }
}
