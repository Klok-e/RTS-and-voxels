using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.Help
{
    public static class MathUtils
    {
        public static Vector3 ToVec(this int3 x)
        {
            return new Vector3(x.x, x.y, x.z);
        }

        public static Vector3Int ToVecInt(this int3 x)
        {
            return new Vector3Int(x.x, x.y, x.z);
        }

        public static float3 ToFloat(this Vector3 x)
        {
            return new float3(x.x, x.y, x.z);
        }

        public static int3 ToInt(this Vector3Int x)
        {
            return new int3(x.x, x.y, x.z);
        }
    }
}
