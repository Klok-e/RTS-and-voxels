using Scripts.Help;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Scripts.World
{
    public struct Voxel
    {
        public VoxelType type;
    }

    public enum VoxelType : byte
    {
        Air = 0,
        Solid = 1,
    }

    public enum BlittableBool : byte
    {
        False = 0,
        True = 1,
    }

    public static class VoxelExtensions
    {
        public static Color[] colors;

        public static Color ToColor(this Voxel vox)
        {
            try
            {
                return colors[(byte)vox.type];
            }
            catch (IndexOutOfRangeException)
            {
                Debug.LogError("Color not in the list!");
                return Color.magenta;
            }
        }

        public static Vector3 ToVector3(this Color col)
        {
            return new Vector3(col.r, col.g, col.b);
        }

        public static Color ToColor(this Vector3 vec)
        {
            return new Color(vec.x, vec.y, vec.z, 1);
        }

        public static bool IsAir(this VoxelType type)
        {
            if (type == VoxelType.Air)
                return true;
            else
                return false;
        }
    }
}
