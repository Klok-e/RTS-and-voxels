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
        public NativeArray3D<Voxel> Voxels { get; private set; }
        public NativeMeshData MeshData { get; private set; }
        public NativeQueue<VoxelSetQueryData> VoxelSetQuery { get; private set; }

        public bool IsInitialized { get; private set; }

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MeshCollider _coll;

        public RegularChunk _left;
        public RegularChunk _right;
        public RegularChunk _up;
        public RegularChunk _down;
        public RegularChunk _forward;
        public RegularChunk _backward;

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
            Voxels = new NativeArray3D<Voxel>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            VoxelSetQuery = new NativeQueue<VoxelSetQueryData>(Allocator.Persistent);
        }

        public void Deinitialize()
        {
            name = "Chunk Inactive";
            IsInitialized = false;
            gameObject.SetActive(false);

            VoxelsVisibleFaces.Dispose();
            Voxels.Dispose();
            MeshData.Dispose();
            VoxelLightLevels.Dispose();

            _left = null;
            _right = null;
            _up = null;
            _down = null;
            _forward = null;
            _backward = null;
        }

        public RegularChunk this[DirectionsHelper.BlockDirectionFlag dir]
        {
            get
            {
                switch(dir)
                {
                    case DirectionsHelper.BlockDirectionFlag.Up:
                        return _up;
                    case DirectionsHelper.BlockDirectionFlag.Down:
                        return _down;
                    case DirectionsHelper.BlockDirectionFlag.Left:
                        return _left;
                    case DirectionsHelper.BlockDirectionFlag.Right:
                        return _right;
                    case DirectionsHelper.BlockDirectionFlag.Backward:
                        return _backward;
                    case DirectionsHelper.BlockDirectionFlag.Forward:
                        return _forward;
                    case DirectionsHelper.BlockDirectionFlag.None:
                        return null;
                    default:
                        return null;
                }
            }
            set
            {
                switch(dir)
                {
                    case DirectionsHelper.BlockDirectionFlag.Up:
                        _up = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Down:
                        _down = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Left:
                        _left = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Right:
                        _right = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Backward:
                        _backward = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Forward:
                        _forward = value;
                        break;
                }
            }
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
