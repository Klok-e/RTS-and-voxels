using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.World
{
    internal class ChunkContainer
    {
        private Dictionary<int, Chunk[,]> _chunks;
        private int _mapLength;
        private int _mapWidth;

        public ChunkContainer(int mapLength, int mapWidth)
        {
            _mapLength = mapLength;
            _mapWidth = mapWidth;
            _chunks = new Dictionary<int, Chunk[,]>();
        }

        public void Remove(int height)
        {
            _chunks.Remove(height);
        }

        public bool ContainsHeight(int height)
        {
            return _chunks.ContainsKey(height);
        }

        public Chunk[,] this[int height]
        {
            get
            {
                return _chunks[height];
            }
            set
            {
                if (_chunks.ContainsKey(height))
                {
                    _chunks[height] = value;
                }
                else
                {
                    Debug.Assert(value.GetLength(0) == _mapLength && value.GetLength(1) == _mapWidth, "Dimensions don't match");
                    _chunks.Add(height, value);
                }
            }
        }
    }
}
