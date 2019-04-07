using Scripts.World.DynamicBuffers;
using Unity.Entities;
using UnityEngine;

namespace Scripts.World.Utils
{
    public static class VoxelExtensions
    {
        public static Vector3 ToVector3(this Color col)
        {
            return new Vector3(col.r, col.g, col.b);
        }

        public static Color ToColor(this Vector3 vec)
        {
            return new Color(vec.x, vec.y, vec.z);
        }

        public static bool IsEmpty(this VoxelType type)
        {
            if(type == VoxelType.Empty)
                return true;
            else
                return false;
        }

        public static Voxel AtGet(this DynamicBuffer<Voxel> buffer, int x, int y, int z)
        {
            const int sz = VoxConsts._chunkSize;
            return buffer[z * sz * sz + y * sz + x];
        }

        public static void AtSet(this DynamicBuffer<Voxel> buffer, int x, int y, int z, Voxel value)
        {
            const int sz = VoxConsts._chunkSize;
            buffer[z * sz * sz + y * sz + x] = value;
        }

        public static void AtAt(this DynamicBuffer<Voxel> buffer, int i, out int x, out int y, out int z)
        {
            const int sz = VoxConsts._chunkSize;
            x = i % sz;
            y = (i / sz) % sz;
            z = i / (sz * sz);
        }

        public static VoxelLightingLevel AtGet(this DynamicBuffer<VoxelLightingLevel> buffer, int x, int y, int z)
        {
            const int sz = VoxConsts._chunkSize;
            return buffer[z * sz * sz + y * sz + x];
        }

        public static void AtSet(this DynamicBuffer<VoxelLightingLevel> buffer, int x, int y, int z, VoxelLightingLevel value)
        {
            const int sz = VoxConsts._chunkSize;
            buffer[z * sz * sz + y * sz + x] = value;
        }

        public static void AtAt(this DynamicBuffer<VoxelLightingLevel> buffer, int i, out int x, out int y, out int z)
        {
            const int sz = VoxConsts._chunkSize;
            x = i % sz;
            y = (i / sz) % sz;
            z = i / (sz * sz);
        }
    }
}
