using System;
using Unity.Entities;
using Unity.Mathematics;
using World.Components;
using World.DynamicBuffers;

namespace World.Utils
{
    internal static class VoxelInteractionUtils
    {
        [Obsolete]
        public static void SetQuerySphere(Entity    entity, EntityManager manager, int3 index, uint radius,
                                          VoxelType voxType)
        {
            var setVox   = manager.GetBuffer<VoxelSetQueryData>(entity);
            var setLight = manager.GetBuffer<LightSetQueryData>(entity);

            setVox.Add(new VoxelSetQueryData {NewVoxelType = voxType, Pos = index});

            setLight.Add(new LightSetQueryData
                {LightType = SetLightType.RegularLight, NewLight = voxType.GetLight(), Pos = index});
            setLight.Add(new LightSetQueryData {LightType = SetLightType.Sunlight, NewLight = 0, Pos = index});

            manager.AddComponentData(entity, new ChunkNeedApplyVoxelChanges());
        }
    }
}