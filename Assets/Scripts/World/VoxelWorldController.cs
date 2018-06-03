using Plugins.Helpers;
using Scripts.World.Jobs;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.World
{
    public class VoxelWorldController : MonoBehaviour
    {
        [SerializeField] private int _mapLength, _mapWidth;

        [SerializeField] private Material _material;

        [SerializeField] private Color[] _colors;

        private Queue<ChunkUpdateData> _updateDataToProcess;
        private Queue<RegularChunk> _chunksToApplyChanges;

        private void Awake()
        {
            _updateDataToProcess = new Queue<ChunkUpdateData>();
            _chunksToApplyChanges = new Queue<RegularChunk>();

            VoxelExtensions.colors = _colors;
            RegularChunk._material = _material;
            RegularChunk._chunkParent = transform;

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
                //int count = VoxelWorld.Instance.Dirty.Count > (System.Environment.ProcessorCount - 1) ? (System.Environment.ProcessorCount - 1) : VoxelWorld.Instance.Dirty.Count;

                var ch = VoxelWorld.Instance.Dirty.Dequeue();
                var data = VoxelWorld.Instance.CleanChunk(ch);
                _updateDataToProcess.Enqueue(data);
            }

            if (_chunksToApplyChanges.Count > 0)
            {
                var chunk = _chunksToApplyChanges.Dequeue();
                chunk.ApplyMeshData();
            }

            if (_updateDataToProcess.Count > 0)
            {
                var data = _updateDataToProcess.Dequeue();
                VoxelWorld.Instance.CompleteChunkUpdate(data);
                _chunksToApplyChanges.Enqueue(data._chunk);
            }
        }

        private void OnApplicationQuit()
        {
        }

        public void GenerateLevel(bool isUp)
        {
            VoxelWorld.Instance.GenerateLevel(isUp);
        }
    }
}
