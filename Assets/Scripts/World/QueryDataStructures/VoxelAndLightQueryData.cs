using UnityEngine;

namespace Scripts.World.QueryDataStructures
{
    public struct VoxelSetQueryData
    {
        public Vector3Int Pos { get; set; }
        public VoxelType NewVoxelType { get; set; }
    }

    public struct LightChangeQueryData
    {
        public Vector3 worldPos;
        public int level;
    }
}
