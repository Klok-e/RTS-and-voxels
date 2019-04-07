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
            var setVox = manager.GetBuffer<VoxelSetQueryData>(entity);
            var setLight = manager.GetBuffer<LightSetQueryData>(entity);

            setVox.Add(new VoxelSetQueryData { NewVoxelType = voxType, Pos = index });
            if(voxType.IsEmpty())
                setLight.Add(new LightSetQueryData { LightType = SetLightType.RegularLight, NewLight = 0, Pos = index, Propagation = PropagationType.Regular, });
            else
                setLight.Add(new LightSetQueryData { LightType = SetLightType.RegularLight, NewLight = 15, Pos = index, Propagation = PropagationType.Regular, });

            manager.AddComponentData(entity, new ChunkNeedApplyVoxelChanges());
        }
    }
}
