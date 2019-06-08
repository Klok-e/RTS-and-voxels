using Scripts.Help;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Scripts.World.Systems.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Systems.Interaction
{
    [UpdateBefore(typeof(ApplyVoxelChangesSystem))]
    public class ChangeTerrainVoxelSystem : ComponentSystem
    {
        public struct SetPos
        {
            public int3 Chunk;
            public int3 Coord;
            public VoxelType VoxelType;
        }

        public Queue<SetPos> ToSetPos { get; private set; }

        private RegionLoadUnloadSystem _regionLoadUnloadSystem;

        protected override void OnCreate()
        {
            ToSetPos = new Queue<SetPos>();
            _regionLoadUnloadSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
        }

        protected override void OnUpdate()
        {
            while(ToSetPos.Count > 0)
            {
                var curr = ToSetPos.Dequeue();

                Check(curr);

                var ent = _regionLoadUnloadSystem.PosToChunkEntity[curr.Chunk];
                var voxSet = EntityManager.GetBuffer<VoxelSetQueryData>(ent);
                var lightSet = EntityManager.GetBuffer<LightSetQueryData>(ent);

                voxSet.Add(new VoxelSetQueryData
                {
                    NewVoxelType = curr.VoxelType,
                    Pos = curr.Coord,
                });
                lightSet.Add(new LightSetQueryData
                {
                    LightType = SetLightType.RegularLight,
                    NewLight = curr.VoxelType.GetLight(),
                    Pos = curr.Coord,
                });
                lightSet.Add(new LightSetQueryData
                {
                    LightType = SetLightType.Sunlight,
                    NewLight = 0,
                    Pos = curr.Coord,
                });

                PostUpdateCommands.AddComponent(ent, new ChunkNeedApplyVoxelChanges());
            }
        }

        private void Check(SetPos pos)
        {
            var p = pos.Coord;
            Debug.Assert(!DirectionsHelper.AreCoordsOutOfBordersOfChunk(p.x, p.y, p.z));
        }
    }
}
