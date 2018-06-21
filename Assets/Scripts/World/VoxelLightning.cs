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
        private const int regularLightMask = 0b0000_1111;
        private const int sunLightMask = 0b1111_0000;

        /// <summary>
        /// [sulight]xxxx || [regular light]xxxx
        /// </summary>
        public byte _level;

        public const int maxLight = 15;

        public int RegularLight
        {
            get
            {
                return (byte)((_level & regularLightMask));
            }
            set
            {
                _level &= sunLightMask;
                _level |= (byte)(regularLightMask & value);
            }
        }

        public int Sunlight
        {
            get
            {
                return (byte)((_level & sunLightMask) >> 4);
            }
            set
            {
                _level &= regularLightMask;
                _level |= (byte)(sunLightMask & (value << 4));
            }
        }

        public bool IsAnyLightPresent
        {
            get { return _level > 0; }
        }

        public VoxelLightingLevel(int light, int sunlight)
        {
            _level = 0;
            RegularLight = light;
            Sunlight = sunlight;
        }
    }

    public struct VoxelLightPropagationData
    {
        public Vector3Int _blockPos;
        public Vector3Int _chunkPos;
    }
}
