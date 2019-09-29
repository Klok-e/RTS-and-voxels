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

                var ent      = _regionLoadUnloadSystem.PosToChunkEntity[curr.Chunk];
                var voxSet   = EntityManager.GetBuffer<VoxelSetQueryData>(ent);
                var lightSet = EntityManager.GetBuffer<LightSetQueryData>(ent);

                voxSet.Add(new VoxelSetQueryData
                {
                    NewVoxelType = curr.VoxelType,
                    Pos          = curr.Coord
                });
                lightSet.Add(new LightSetQueryData
                {
                    LightType = SetLightType.RegularLight,
                    NewLight  = curr.VoxelType.GetLight(),
                    Pos       = curr.Coord
                });
                lightSet.Add(new LightSetQueryData
                {
                    LightType = SetLightType.Sunlight,
                    NewLight  = 0,
                    Pos       = curr.Coord
                });

                PostUpdateCommands.AddComponent(ent, new ChunkNeedApplyVoxelChanges());
            }
        }

        private void Check(SetPos pos)
        {
            var p = pos.Coord;
            Debug.Assert(!DirectionsHelper.AreCoordsOutOfBordersOfChunk(p.x, p.y, p.z));
        }

        public struct SetPos
        {
            public int3      Chunk;
            public int3      Coord;
            public VoxelType VoxelType;
        }
    }
}