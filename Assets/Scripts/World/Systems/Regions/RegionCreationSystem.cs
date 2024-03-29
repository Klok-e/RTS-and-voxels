﻿using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using World.Components;

namespace World.Systems.Regions
{
    public class RegionCreationSystem : ComponentSystem
    {
        private int3 _loaderRegionInPrev;

        private EntityCommandBufferSystem _barrier;

        public NativeHashMap<int3, Entity> Regions { get; private set; }

        protected override void OnCreate()
        {
            Regions             = new NativeHashMap<int3, Entity>(100, Allocator.Persistent);
            _loaderRegionInPrev = math.int3(3);

            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnDestroy()
        {
            Regions.Dispose();
        }

        protected override void OnUpdate()
        {
            //var commands = _barrier.CreateCommandBuffer();

            //Regions.Clear();
            //Entities.ForEach((Entity ent, ref RegionPosComponent pos) =>
            //{
            //    if (!Regions.TryAdd(pos.Pos, ent))
            //        throw new Exception("Could not add region to Regions");
            //});

            Entities.ForEach((ref MapLoader loader, ref LocalToWorld pos) =>
            {
                var regionLoaderIn = VoxConsts.RegionIn(pos.Position);
                if (!math.any(_loaderRegionInPrev != regionLoaderIn)) return;

                _loaderRegionInPrev = regionLoaderIn;

                // gen new
                for (int x = -loader.RegionDistance; x <= loader.RegionDistance; x++)
                for (int y = -loader.RegionDistance; y <= loader.RegionDistance; y++)
                for (int z = -loader.RegionDistance; z <= loader.RegionDistance; z++)
                {
                    var regPos = new int3(x, y, z) + regionLoaderIn;
                    if (!Regions.TryGetValue(regPos, out _))
                        CreateRegion(regPos);
                }

                // prune old
                using (var keys = Regions.GetKeyArray(Allocator.Temp))
                    foreach (var key in keys)
                        if (math.distance(regionLoaderIn, key) > loader.RegionDistance)
                            RemoveRegion(key);
            });
        }

        private void CreateRegion(int3 pos)
        {
            var ent = EntityManager.CreateEntity();
            if (!Regions.TryAdd(pos,ent))
                Debug.LogError("Could not add region to Regions");

            EntityManager.AddComponentData(ent, new RegionNeedLoadComponentTag());
            EntityManager.AddComponentData(ent, new RegionPosComponent {Pos = pos});
            EntityManager.AddBuffer<RegionChunks>(ent);
        }

        private void RemoveRegion(int3 pos)
        {
            EntityManager.AddComponentData(Regions[pos], new RegionNeedUnloadComponentTag());
            Regions.Remove(pos);
        }
    }
}