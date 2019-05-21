using Scripts.World.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Scripts.World.Systems
{
    public class RegionCreationSystem : ComponentSystem
    {
        public NativeHashMap<int3, Entity> Regions { get; private set; }
        private int3 _loaderRegionInPrev;

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
            Regions.Clear();
            Entities.ForEach((Entity ent, ref RegionPosComponent pos) =>
            {
                if(!Regions.TryAdd(pos.Pos, ent))
                    throw new Exception("Could not add region to Regions");
            });

            Entities.ForEach((ref MapLoader loader, ref LocalToWorld pos) =>
            {
                var regionLoaderIn = VoxConsts.RegionIn(pos.Position);
                if(math.any(_loaderRegionInPrev != regionLoaderIn))
                {
                    _loaderRegionInPrev = regionLoaderIn;

                    // gen new
                    for(int x = -loader.RegionDistance; x <= loader.RegionDistance; x++)
                        for(int y = -loader.RegionDistance; y <= loader.RegionDistance; y++)
                            for(int z = -loader.RegionDistance; z <= loader.RegionDistance; z++)
                            {
                                var chPos = new int3(x, y, z) + regionLoaderIn;
                                if(!Regions.TryGetValue(chPos, out var _))
                                    CreateRegion(chPos);
                            }

                    // prune old
                    using(var keys = Regions.GetKeyArray(Allocator.Temp))
                        foreach(var key in keys)
                        {
                            if(math.distance(regionLoaderIn, key) > loader.RegionDistance)
                                RemoveRegion(key);
                        }
                }
            });
        }

        private void CreateRegion(int3 pos)
        {
            var ent = PostUpdateCommands.CreateEntity();

            PostUpdateCommands.AddComponent(ent, new RegionNeedLoadComponentTag());
            PostUpdateCommands.AddComponent(ent, new RegionPosComponent { Pos = pos });
            PostUpdateCommands.AddBuffer<RegionChunks>(ent);
        }

        private void RemoveRegion(int3 pos)
        {
            PostUpdateCommands.AddComponent(Regions[pos], new RegionNeedUnloadComponentTag());
        }
    }
}
