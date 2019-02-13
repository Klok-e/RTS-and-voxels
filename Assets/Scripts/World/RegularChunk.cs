using Scripts.Help.DataContainers;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts.World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class RegularChunk : MonoBehaviour
    {
        public NativeMeshData MeshData { get; private set; }

        public bool IsInitialized { get; private set; }

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MeshCollider _coll;

        public void Initialize(int3 pos, Material material)
        {
            var newpos = math.float3(pos) * VoxConsts._chunkSize * VoxConsts._blockSize;
            transform.position = newpos;
            gameObject.SetActive(true);
            name = $"Chunk Active at {pos}";
            IsInitialized = true;
            _renderer.material = material;

            MeshData = new NativeMeshData(0, Allocator.Persistent);
        }

        public void Deinitialize()
        {
            name = "Chunk Inactive";
            IsInitialized = false;
            gameObject.SetActive(false);

            MeshData.Dispose();
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

        private void OnDestroy()
        {
            Deinitialize();
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
