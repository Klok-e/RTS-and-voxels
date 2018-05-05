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

namespace Scripts.World
{
    public class RegularChunk : MonoBehaviour
    {
        public static Material _material;
        public static Transform _chunkParent;

        public Vector3Int Pos { get; private set; }

        public NativeArray3D<DirectionsHelper.BlockDirectionFlag> VoxelsVisibleFaces { get; private set; }
        public NativeArray3D<BlittableBool> VoxelsIsVisible { get; private set; }
        public NativeArray3D<Voxel> Voxels { get; private set; }

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// To mark border chunks
        /// </summary>
        public bool IsPlaceholder { get; set; }

        public NativeMeshData MeshData { get; private set; }

        private Mesh mesh;
        private MeshFilter filter;
        private MeshCollider coll;

        public void Initialize(Vector3Int pos)
        {
            IsPlaceholder = true;
            Pos = pos;

            transform.position = (Vector3)Pos * VoxelWorld._chunkSize * VoxelWorld._blockSize;
            gameObject.SetActive(true);
            name = $"Chunk Active at {pos}";
            IsInitialized = true;

            mesh = new Mesh();
            mesh.MarkDynamic();
        }

        public void Deinitialize()
        {
            name = "Chunk Inactive";
            IsInitialized = false;
            gameObject.SetActive(false);
        }

        public void ApplyMeshData()
        {
            if (filter == null)
            {
                filter = GetComponent<MeshFilter>();
                filter.sharedMesh = mesh;
            }
            if (coll == null)
            {
                coll = GetComponent<MeshCollider>();
                coll.sharedMesh = mesh;
            }

            mesh.Clear();
            mesh.vertices = MeshData._vertices.ToArray();
            mesh.SetTriangles(MeshData._triangles.ToArray(), 0);
            mesh.normals = MeshData._normals.ToArray();
            mesh.colors32 = MeshData._colors.ToArray();

            coll.sharedMesh = mesh;
        }

        private void Awake()
        {
            MeshData = new NativeMeshData(10000, Allocator.Persistent);
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
            chunkObj.GetComponent<Renderer>().material = RegularChunk._material;
            return chunkObj;
        }
    }
}
