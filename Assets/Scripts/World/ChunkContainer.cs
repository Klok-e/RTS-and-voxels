using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace World
{
    [Obsolete]
    public class ChunkContainer : IEnumerable<RegularChunk>
    {
        private readonly List<RegularChunk[,]> _chunks;
        private readonly int                   _mapLength;
        private readonly int                   _mapWidth;

        public ChunkContainer(int mapLength, int mapWidth)
        {
            _mapLength = mapLength;
            _mapWidth  = mapWidth;
            _chunks    = new List<RegularChunk[,]>();
        }

        public int MaxHeight => _chunks.Count - 1 + MinHeight;
        public int MinHeight { get; private set; }

        public bool IsInitialized { get; private set; }

        public RegularChunk[,] this[int height]
        {
            get
            {
#if UNITY_EDITOR
                if (!IsInitialized) Debug.LogError("Wasn't initialized");
#endif
                return _chunks[height - MinHeight];
            }
        }

        public IEnumerator<RegularChunk> GetEnumerator()
        {
            for (int i = 0; i < _chunks.Count; i++)
            for (int x = 0; x < _mapWidth; x++)
            for (int y = 0; y < _mapLength; y++)
                yield return _chunks[i][x, y];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < _chunks.Count; i++)
            for (int x = 0; x < _mapWidth; x++)
            for (int y = 0; y < _mapLength; y++)
                yield return _chunks[i][x, y];
        }

        public bool ContainsHeight(int height)
        {
#if UNITY_EDITOR
            if (!IsInitialized) Debug.LogError("Wasn't initialized");
#endif
            return height - MinHeight < _chunks.Count && height - MinHeight >= 0;
        }

        public void InitializeStartingLevel(int height, RegularChunk[,] chunks)
        {
            _chunks.Add(chunks);
            MinHeight = height;

            IsInitialized = true;
        }

        public void RemoveLevel(bool isUp)
        {
#if UNITY_EDITOR
            if (!IsInitialized)
            {
                Debug.LogError("Wasn't initialized");
                return;
            }
#endif
            if (isUp)
            {
                _chunks.RemoveAt(_chunks.Count - 1);
            }
            else
            {
                _chunks.RemoveAt(0);
                MinHeight += 1;
            }
        }

        public void AddLevel(bool isUp, RegularChunk[,] chunks)
        {
#if UNITY_EDITOR
            if (!IsInitialized)
            {
                Debug.LogError("Wasn't initialized");
                return;
            }

            if (chunks.GetLength(0) != _mapLength || chunks.GetLength(1) != _mapWidth)
            {
                Debug.LogError("Dimensions don't match");
                return;
            }
#endif
            if (isUp)
            {
                _chunks.Insert(_chunks.Count, chunks);
            }
            else
            {
                _chunks.Insert(0, chunks);
                MinHeight -= 1;
            }
        }
    }
}