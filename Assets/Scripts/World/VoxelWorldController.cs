using Scripts.Help;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Plugins.Helpers;
using Unity.Jobs;
using ProceduralNoiseProject;
using Unity.Collections;

namespace Scripts.World
{
    public class VoxelWorldController : MonoBehaviour
    {
        [SerializeField] private int _mapLength, _mapWidth;

        [SerializeField] private float _blockSize;

        [SerializeField] private Material _material;

        [SerializeField] private Color32[] _colors;

        private JobHandle _handle;
        private bool _isProcessing;

        private byte _finishedProcessingCount;
        private Queue<RegularChunk> _chunksToProcess;

        private void Awake()
        {
            _chunksToProcess = new Queue<RegularChunk>();

            VoxelExtensions.colors = _colors;
            RegularChunk._material = _material;

            UnityThread.InitUnityThread();
            VoxelWorld.Instance.Initialize(_mapLength, _mapWidth, transform);
        }

        private void Start()
        {
        }

        private void Update()
        {
            if (VoxelWorld.Instance.Dirty.Count > 0)
            {
                var ch = VoxelWorld.Instance.Dirty.Dequeue();
                _handle = VoxelWorld.Instance.CleanChunk(ch);

                _chunksToProcess.Enqueue(ch);
                _isProcessing = true;
            }

            if (_finishedProcessingCount > 0 && _chunksToProcess.Count > 0)
            {
                _chunksToProcess.Dequeue().ApplyMeshData();
                _finishedProcessingCount--;
            }
            if (_isProcessing)
            {
                _handle.Complete();
                _finishedProcessingCount++;
                _isProcessing = false;
            }
        }

        private void OnApplicationQuit()
        {
        }
    }
}
