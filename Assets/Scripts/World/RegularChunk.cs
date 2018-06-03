using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Help;
using ProceduralNoiseProject;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine.AI;

namespace Scripts.World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class RegularChunk : MonoBehaviour
    {
        public static Material _material;
        public static Transform _chunkParent;

        public Vector3Int Pos { get; private set; }

        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> VoxelsVisibleFaces { get; private set; }
        public NativeArray3D<BlittableBool> VoxelsIsVisible { get; private set; }
        public NativeArray3D<VoxelLightingLevel> VoxelLightingLevels { get; private set; }
        public NativeArray3D<Voxel> Voxels { get; private set; }

        public bool IsInitialized { get; private set; }

        public NativeMeshData MeshData { get; private set; }

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MeshCollider _coll;

        public void Initialize(Vector3Int pos)
        {
            Pos = pos;

            transform.position = (Vector3)Pos * VoxelWorld._chunkSize * VoxelWorld._blockSize;
            gameObject.SetActive(true);
            name = $"Chunk Active at {pos}";
            IsInitialized = true;
        }

        public void Deinitialize()
        {
            name = "Chunk Inactive";
            IsInitialized = false;
            gameObject.SetActive(false);
        }

        public void ApplyMeshData()
        {
            _mesh.Clear();
            _mesh.vertices = MeshData._vertices.ToArray();
            _mesh.SetTriangles(MeshData._triangles.ToArray(), 0);
            _mesh.normals = MeshData._normals.ToArray();
            _mesh.colors = MeshData._colors.ToArray();
            MeshData.Clear();

            _filter.sharedMesh = _mesh;
            _coll.sharedMesh = _mesh;
        }

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _coll = GetComponent<MeshCollider>();
            _mesh = new Mesh();
            _mesh.MarkDynamic();

            _renderer.material = _material;

            MeshData = new NativeMeshData(0, Allocator.Persistent);
            VoxelLightingLevels = new NativeArray3D<VoxelLightingLevel>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            VoxelsIsVisible = new NativeArray3D<BlittableBool>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            VoxelsVisibleFaces = new NativeArray3D<DirectionsHelper.BlockDirectionFlag>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            Voxels = new NativeArray3D<Voxel>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            VoxelsIsVisible.Dispose();
            VoxelsVisibleFaces.Dispose();
            Voxels.Dispose();
            MeshData.Dispose();
            VoxelLightingLevels.Dispose();
        }

        public static RegularChunk CreateNew()
        {
            RegularChunk chunkObj;
            var go = new GameObject("Chunk");
            go.transform.parent = _chunkParent;

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            chunkObj = go.AddComponent<RegularChunk>();

            return chunkObj;
        }
    }
}
