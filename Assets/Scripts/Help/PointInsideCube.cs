using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Help
{
    internal static class PointInsideCube
    {
        public static bool IsInsideCube(Vector3 point, Vector3 cubePos, int cubeSize)
        {
            var minCorner = cubePos - Vector3.one * cubeSize;
            var maxCorner = cubePos + Vector3.one * cubeSize;
            if (minCorner.x < point.x && minCorner.y < point.y && minCorner.z < point.z
                &&
                maxCorner.x > point.x && maxCorner.y > point.y && maxCorner.z > point.z)
            {
                return true;
            }
            return false;
        }
    }
}
