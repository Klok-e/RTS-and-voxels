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
                {lightType = SetLightType.RegularLight, newLight = voxType.GetLight(), pos = index});
            setLight.Add(new LightSetQueryData {lightType = SetLightType.Sunlight, newLight = 0, pos = index});

            manager.AddComponentData(entity, new ChunkNeedApplyVoxelChanges());
        }
    }
}