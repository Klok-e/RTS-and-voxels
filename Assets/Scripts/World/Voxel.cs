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
        public static Color32[] colors;

        public static Color32 ToColor(this Voxel vox, BlittableBool isVisible)
        {
            try
            {
                if (isVisible == BlittableBool.False)
                    return Color.black;

                return colors[(byte)vox.type];
            }
            catch (IndexOutOfRangeException)
            {
                Debug.LogError("Color not in the list!");
                return Color.magenta;
            }
        }

        public static bool IsTransparent(this VoxelType type)
        {
            if (type == VoxelType.Air)
                return true;
            else
                return false;
        }
    }
}
