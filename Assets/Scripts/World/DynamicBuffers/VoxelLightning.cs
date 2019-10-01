using Unity.Entities;
using UnityEngine;

namespace World.DynamicBuffers
{
    /// <summary>
    ///     Levels of light
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct VoxelLightingLevel : IBufferElementData
    {
        private const int regularLightMask = 0b0000_1111;
        private const int sunLightMask     = 0b1111_0000;

        /// <summary>
        ///     [sulight]xxxx || [regular light]xxxx
        /// </summary>
        public byte Level;

        public const int MaxLight = 15;

        public int RegularLight
        {
            get => (byte) (Level & regularLightMask);
            set
            {
                Level &= sunLightMask;
                Level |= (byte) (regularLightMask & value);
            }
        }

        public int Sunlight
        {
            get => (byte) ((Level & sunLightMask) >> 4);
            set
            {
                Level &= regularLightMask;
                Level |= (byte) (sunLightMask & (value << 4));
            }
        }

        public bool IsAnyLightPresent => Level > 0;

        public VoxelLightingLevel(int light, int sunlight)
        {
            Level        = 0;
            RegularLight = light;
            Sunlight     = sunlight;
        }
    }

    public struct VoxelLightPropagationData
    {
        public Vector3Int _blockPos;
        public Vector3Int _chunkPos;
    }
}