using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems.Regions
{
    public class RegionLoadUnloadSystem : ComponentSystem
    {

        protected override void OnUpdate()
        {
            // load
            Entities.WithAll<RegionNeedLoadComponentTag>().ForEach((Entity ent, DynamicBuffer<RegionChunks> chunks, ref RegionPosComponent regionPos) =>
            {
                if(!TryFindRegion(regionPos.Pos))
                {
                    for(int z = 0; z < VoxConsts._regionSize; z++)
                        for(int y = 0; y < VoxConsts._regionSize; y++)
                            for(int x = 0; x < VoxConsts._regionSize; x++)
                            {
                                CreateChunk(regionPos.Pos * VoxConsts._regionSize + math.int3(x, y, z));
                            }
                }
            });

            // unload
            Entities.WithAll<RegionNeedUnloadComponentTag>().ForEach((Entity ent, ref RegionPosComponent pos) =>
            {

            });
        }

        private void CreateChunk(int3 pos)
        {
            var ent = PostUpdateCommands.CreateEntity();

            PostUpdateCommands.AddComponent(ent, new ChunkNeedTerrainGeneration());
            PostUpdateCommands.AddComponent(ent, new ChunkPosComponent { Pos = pos, });

            var buf1 = PostUpdateCommands.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            var buf2 = PostUpdateCommands.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            PostUpdateCommands.AddBuffer<VoxelSetQueryData>(ent);
            PostUpdateCommands.AddBuffer<LightSetQueryData>(ent);
        }

        // TODO: this
        private bool TryFindRegion(int3 pos)
        {
            return false;
        }

        // TODO: this
        private void SaveRegion(int3 pos)
        {

        }
    }
}
