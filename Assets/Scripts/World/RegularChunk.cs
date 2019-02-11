using Scripts.Help;
using Scripts.Help.DataContainers;
using Scripts.World.QueryDataStructures;
using Unity.Collections;
using UnityEngine;

namespace Scripts.World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class RegularChunk : MonoBehaviour
    {
        public Vector3Int Pos { get; private set; }

        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> VoxelsVisibleFaces { get; private set; }
        public NativeArray3D<VoxelLightingLevel> VoxelLightLevels { get; private set; }
        public NativeMeshData MeshData { get; private set; }
        public NativeQueue<VoxelSetQueryData> VoxelSetQuery { get; private set; }

        public bool IsInitialized { get; private set; }

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MeshCollider _coll;

        public void Initialize(Vector3Int pos, Material material)
        {
            Pos = pos;

            transform.position = (Vector3)Pos * VoxelWorld._chunkSize * VoxelWorld._blockSize;
            gameObject.SetActive(true);
            name = $"Chunk Active at {pos}";
            IsInitialized = true;
            _renderer.material = material;

            MeshData = new NativeMeshData(0, Allocator.Persistent);
            VoxelLightLevels = new NativeArray3D<VoxelLightingLevel>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            VoxelsVisibleFaces = new NativeArray3D<DirectionsHelper.BlockDirectionFlag>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            VoxelSetQuery = new NativeQueue<VoxelSetQueryData>(Allocator.Persistent);
        }

        public void Deinitialize()
        {
            name = "Chunk Inactive";
            IsInitialized = false;
            gameObject.SetActive(false);

            VoxelsVisibleFaces.Dispose();
            MeshData.Dispose();
            VoxelLightLevels.Dispose();
            VoxelSetQuery.Dispose();
        }

        public void ApplyMeshData()
        {
            _mesh.Clear();
            _mesh.vertices = MeshData._vertices.ToArray();
            _mesh.SetTriangles(MeshData._triangles.ToArray(), 0);
            _mesh.normals = MeshData._normals.ToArray();
            _mesh.colors = MeshData._colors.ToArray();
            _mesh.uv = MeshData._uv.ToArray();
            _mesh.uv2 = MeshData._uv2.ToArray();
            _mesh.uv3 = MeshData._uv3.ToArray();
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
        }

        public static RegularChunk CreateNew()
        {
            RegularChunk chunkObj;
            var go = new GameObject("Chunk");

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            chunkObj = go.AddComponent<RegularChunk>();

            return chunkObj;
        }
    }
}
