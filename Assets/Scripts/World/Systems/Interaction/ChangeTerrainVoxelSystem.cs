using System.Collections.Generic;
using Help;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using World.Components;
using World.DynamicBuffers;
using World.Systems.ChunkHandling;
using World.Systems.Regions;

namespace World.Systems.Interaction
{
    [UpdateBefore(typeof(ApplyVoxelChangesSystem))]
    public class ChangeTerrainVoxelSystem : ComponentSystem
    {
        private RegionLoadUnloadSystem _regionLoadUnloadSystem;

        public Queue<SetPos> ToSetPos { get; private set; }

        protected override void OnCreate()
        {
            ToSetPos                = new Queue<SetPos>();
            _regionLoadUnloadSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
        }

        protected override void OnUpdate()
        {
            while (ToSetPos.Count > 0)
            {
                var curr = ToSetPos.Dequeue();

                Check(curr);

                var ent      = _regionLoadUnloadSystem.PosToChunkEntity[curr.chunk];
                var voxSet   = EntityManager.GetBuffer<VoxelSetQueryData>(ent);
                var lightSet = EntityManager.GetBuffer<LightSetQueryData>(ent);

                voxSet.Add(new VoxelSetQueryData
                {
                    NewVoxelType = curr.voxelType,
                    Pos          = curr.coord
                });
                lightSet.Add(new LightSetQueryData
                {
                    lightType = SetLightType.RegularLight,
                    newLight  = curr.voxelType.GetLight(),
                    pos       = curr.coord
                });
                lightSet.Add(new LightSetQueryData
                {
                    lightType = SetLightType.Sunlight,
                    newLight  = 0,
                    pos       = curr.coord
                });

                PostUpdateCommands.AddComponent(ent, new ChunkNeedApplyVoxelChanges());
            }
        }

        private static void Check(SetPos pos)
        {
            var p = pos.coord;
            Debug.Assert(!DirectionsHelper.AreCoordsOutOfBordersOfChunk(p.x, p.y, p.z));
        }

        public struct SetPos
        {
            public int3      chunk;
            public int3      coord;
            public VoxelType voxelType;
        }
    }
}