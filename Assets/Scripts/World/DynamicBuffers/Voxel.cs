using Help;
using Help.DataContainers;
using Unity.Entities;
using UnityEngine;

namespace World.DynamicBuffers
{
    [InternalBufferCapacity(0)]
    public struct Voxel : IBufferElementData
    {
        public VoxelType type;
    }

    public enum VoxelType : byte
    {
        Empty = 0,
        Dirt  = 1,
        Grass = 2,
        Lamp  = 3
    }

    public static class VoxelMeshing
    {
        public static void Mesh(this VoxelType type, DirectionsHelper.BlockDirectionFlag dir, NativeMeshData mesh)
        {
            switch (type)
            {
                case VoxelType.Dirt:
                    mesh._uv2.Add(new Vector2(1, 0));
                    mesh._uv2.Add(new Vector2(1, 0));
                    mesh._uv2.Add(new Vector2(1, 0));
                    mesh._uv2.Add(new Vector2(1, 0));
                    break;

                case VoxelType.Grass:
                    if (dir == DirectionsHelper.BlockDirectionFlag.Up)
                    {
                        mesh._uv2.Add(new Vector2(1, 1));
                        mesh._uv2.Add(new Vector2(1, 1));
                        mesh._uv2.Add(new Vector2(1, 1));
                        mesh._uv2.Add(new Vector2(1, 1));
                    }
                    else
                    {
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                        mesh._uv2.Add(new Vector2(1, 0));
                    }

                    break;

                case VoxelType.Lamp:
                    mesh._uv2.Add(new Vector2(1, 2));
                    mesh._uv2.Add(new Vector2(1, 2));
                    mesh._uv2.Add(new Vector2(1, 2));
                    mesh._uv2.Add(new Vector2(1, 2));
                    break;
            }
        }
    }

    public static class VoxelLightVals
    {
        public static byte GetLight(this VoxelType voxel)
        {
            switch (voxel)
            {
                case VoxelType.Lamp:
                    return VoxelLightingLevel.MaxLight;
                default:
                    return 0;
            }
        }
    }
}