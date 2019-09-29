using UnityEngine;

namespace Help
{
    internal static class Intersections
    {
        public static bool IsInsideCube(Vector3 point, Vector3 cubePos, int cubeSize)
        {
            var minCorner = cubePos - Vector3.one * cubeSize;
            var maxCorner = cubePos + Vector3.one * cubeSize;
            if (minCorner.x < point.x && minCorner.y < point.y && minCorner.z < point.z
                &&
                maxCorner.x > point.x && maxCorner.y > point.y && maxCorner.z > point.z)
                return true;
            return false;
        }

        public static bool CubeVsCubeIntersection(Vector3 firstCenter, float firstHalfWidth, Vector3 secondCenter,
                                                  float   secondHalfWidth)
        {
            bool x = Mathf.Abs(firstCenter.x - secondCenter.x) <= firstHalfWidth + secondHalfWidth;
            bool y = Mathf.Abs(firstCenter.y - secondCenter.y) <= firstHalfWidth + secondHalfWidth;
            bool z = Mathf.Abs(firstCenter.z - secondCenter.z) <= firstHalfWidth + secondHalfWidth;
            return x && y && z;
        }
    }
}