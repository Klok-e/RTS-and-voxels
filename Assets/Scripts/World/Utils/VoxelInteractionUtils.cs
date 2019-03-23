using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World.Utils
{
    internal static class VoxelInteractionUtils
    {
        public static void SetQuerySphere(Entity entity, EntityManager manager, int3 index, uint radius, VoxelType voxType)
        {
            DynamicBuffer<VoxelSetQueryData> buffer;
            if(manager.HasComponent<VoxelSetQueryData>(entity))
                buffer = manager.GetBuffer<VoxelSetQueryData>(entity);
            else
                buffer = manager.AddBuffer<VoxelSetQueryData>(entity);

            buffer.Add(new VoxelSetQueryData { NewVoxelType = voxType, Pos = index });

            manager.AddComponentData(entity, new ChunkNeedApplyVoxelChanges());
        }
    }
}
