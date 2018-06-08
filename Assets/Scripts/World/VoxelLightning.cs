using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.World
{
    /// <summary>
    /// Levels of light
    /// </summary>
    public struct VoxelLightingLevel
    {
        /// <summary>
        /// [unused]xxx || [level]xxxxx
        /// </summary>
        public byte _level;
    }

    public struct VoxelLightPropagationData
    {
        public Vector3Int _blockPos;
        public Vector3Int _chunkPos;
    }
}
