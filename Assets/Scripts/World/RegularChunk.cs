using Help.DataContainers;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class RegularChunk : MonoBehaviour
    {
        private MeshCollider _coll;
        private MeshFilter   _filter;

        private Mesh           _mesh;
        private MeshRenderer   _renderer;
        public  NativeMeshData MeshData { get; private set; }

        public bool IsInitialized { get; private set; }

        public void Initialize(int3 pos, Material material)
        {
            var newpos = math.float3(pos) * VoxConsts.ChunkSize * VoxConsts.BlockSize;

            // shift newpos so that it's actually at a 0,0,0 coords of a chunk assuming it's originally at center
            //newpos -= math.float3(VoxConsts._chunkSize * VoxConsts._blockSize * 0.5f);

            transform.position = newpos;
            gameObject.SetActive(true);
            name               = $"Chunk Active at {pos}";
            IsInitialized      = true;
            _renderer.material = material;

            MeshData = new NativeMeshData(0, Allocator.Persistent);
        }

        public void Deinitialize()
        {
            name          = "Chunk Inactive";
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
            _mesh.colors  = MeshData._colors.ToArray();
            _mesh.uv      = MeshData._uv.ToArray();
            _mesh.uv2     = MeshData._uv2.ToArray();
            _mesh.uv3     = MeshData._uv3.ToArray();
            MeshData.Clear();

            _filter.sharedMesh = _mesh;
            _coll.sharedMesh   = _mesh;
        }

        private void Awake()
        {
            _filter   = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _coll     = GetComponent<MeshCollider>();
            _mesh     = new Mesh();
            _mesh.MarkDynamic();
        }

        private void OnDestroy()
        {
            Deinitialize();
            Destroy(_mesh);
        }

        public static RegularChunk CreateNew()
        {
            RegularChunk chunkObj;
            var          go = new GameObject("Chunk");

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            chunkObj = go.AddComponent<RegularChunk>();

            return chunkObj;
        }
    }
}