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
            Entities.WithAll<RegionNeedLoadComponentTag>().ForEach((Entity ent, ref RegionPosComponent regionPos) =>
            {
                if(!TryFindRegion(regionPos.Pos))
                {
                    PopulateRegion(regionPos.Pos, ent);
                }
                else
                {
                    // TODO: this
                }
            });

            // unload
            var filter = EntityManager.CreateEntityQuery(typeof(RegionNeedLoadComponentTag));
            Entities.WithAll<RegionNeedLoadComponentTag>().ForEach((Entity regionEntity, ref RegionPosComponent regionPos) =>
            {
                SaveRegion(regionPos.Pos);

                // set filter so that only chunks within this region are iterated over
                filter.SetFilter(new ChunkParentRegion
                {
                    ParentRegion = regionEntity,
                });

                // now delete
                Entities.With(filter).ForEach((Entity chunkEntity) =>
                {
                    PostUpdateCommands.DestroyEntity(chunkEntity);
                });

                PostUpdateCommands.DestroyEntity(regionEntity);
            });
        }

        private void PopulateRegion(int3 pos, Entity region)
        {
            for(int z = 0; z < VoxConsts._regionSize; z++)
                for(int y = 0; y < VoxConsts._regionSize; y++)
                    for(int x = 0; x < VoxConsts._regionSize; x++)
                    {
                        CreateChunk(pos * VoxConsts._regionSize + math.int3(x, y, z), region);
                    }
        }

        private void CreateChunk(int3 pos, Entity region)
        {
            var ent = PostUpdateCommands.CreateEntity();

            // archetypes are for weak
            PostUpdateCommands.AddComponent(ent, new ChunkNeedTerrainGeneration());
            PostUpdateCommands.AddComponent(ent, new ChunkPosComponent
            {
                Pos = pos,
            });

            var buf1 = PostUpdateCommands.AddBuffer<Voxel>(ent);
            buf1.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            var buf2 = PostUpdateCommands.AddBuffer<VoxelLightingLevel>(ent);
            buf2.ResizeUninitialized(VoxConsts._chunkSize * VoxConsts._chunkSize * VoxConsts._chunkSize);

            PostUpdateCommands.AddBuffer<VoxelSetQueryData>(ent);
            PostUpdateCommands.AddBuffer<LightSetQueryData>(ent);
            PostUpdateCommands.AddSharedComponent(ent, new ChunkParentRegion { ParentRegion = region, });
        }

        // TODO: this
        private bool TryFindRegion(int3 pos)
        {
            return false;
        }

        // TODO: this, synchronously first then try async
        private void SaveRegion(int3 pos)
        {

        }
    }
}
