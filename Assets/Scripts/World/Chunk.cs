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
    public class Chunk : MonoBehaviour
    {
        public static Material _material;

        public Vector3Int Pos { get; private set; }

        public Array3DNative<DirectionsHelper.BlockDirectionFlag> VoxelsVisibleFaces { get; private set; }
        public Array3DNative<Voxel> Voxels { get; private set; }

        public bool IsInitialized { get; private set; }

        private Mesh mesh;
        private MeshFilter filter;
        private MeshCollider coll;

        public void Initialize(Vector3Int pos)
        {
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

        public void SetMeshData(MeshData data)
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
            mesh.SetVertices(data._vertices);
            mesh.SetTriangles(data._triangles, 0);
            mesh.SetNormals(data._normals);
            mesh.SetColors(data._colors);

            coll.sharedMesh = mesh;
        }

        private void Awake()
        {
            VoxelsVisibleFaces = new Array3DNative<DirectionsHelper.BlockDirectionFlag>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
            Voxels = new Array3DNative<Voxel>(VoxelWorld._chunkSize, VoxelWorld._chunkSize, VoxelWorld._chunkSize, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            VoxelsVisibleFaces.Dispose();
            Voxels.Dispose();
        }

        #region Mesh generation

        public MeshData ConstructMesh()
        {
            var newMesh = new MeshData(new List<Vector3>(), new List<int>(), new List<Color32>(), new List<Vector3>());

            for (int x = 0; x < VoxelWorld._chunkSize; x++)
            {
                for (int y = 0; y < VoxelWorld._chunkSize; y++)
                {
                    for (int z = 0; z < VoxelWorld._chunkSize; z++)
                    {
                        if (Voxels[x, y, z].type != VoxelType.Air)
                        {
                            var col = Voxels[x, y, z].ToColor();
                            CreateCube(newMesh, new Vector3Int(x, y, z), col);
                        }
                    }
                }
            }
            return newMesh;
        }

        private void CreateCube(MeshData mesh, Vector3Int pos, Color32 color)
        {
            var facesVisible = VoxelsVisibleFaces[pos.x, pos.y, pos.z];
            for (int i = 0; i < 6; i++)
            {
                var curr = (DirectionsHelper.BlockDirectionFlag)(1 << i);
                if ((curr & facesVisible) != 0)//0b010 00 & 0b010 00 -> 0b010 00; 0b100 00 & 0b010 00 -> 0b000 00
                    CreateFace(mesh, (Vector3)pos * VoxelWorld._blockSize, curr, color);
            }
        }

        private static void CreateFace(MeshData mesh, Vector3 vertOffset, DirectionsHelper.BlockDirectionFlag dir, Color32 color)
        {
            var startIndex = mesh._vertices.Count;

            Quaternion rotation = Quaternion.identity;

            switch (dir)
            {
                case DirectionsHelper.BlockDirectionFlag.Left: rotation = Quaternion.LookRotation(Vector3.left); break;
                case DirectionsHelper.BlockDirectionFlag.Right: rotation = Quaternion.LookRotation(Vector3.right); break;
                case DirectionsHelper.BlockDirectionFlag.Down: rotation = Quaternion.LookRotation(Vector3.down); break;
                case DirectionsHelper.BlockDirectionFlag.Up: rotation = Quaternion.LookRotation(Vector3.up); break;
                case DirectionsHelper.BlockDirectionFlag.Back: rotation = Quaternion.LookRotation(Vector3.back); break;
                case DirectionsHelper.BlockDirectionFlag.Front: rotation = Quaternion.LookRotation(Vector3.forward); break;
                default: throw new Exception();
            }

            mesh._colors.AddRange(new Color32[] {
                color,
                color,
                color,
                color});

            mesh._vertices.AddRange(new Vector3[] {
                (rotation * (new Vector3(-.5f, -.5f, .5f) * VoxelWorld._blockSize)) + vertOffset,
                (rotation * (new Vector3(.5f, -.5f, .5f) * VoxelWorld._blockSize)) + vertOffset,
                (rotation * (new Vector3(-.5f, .5f, .5f) * VoxelWorld._blockSize)) + vertOffset,
                (rotation * (new Vector3(.5f, .5f, .5f) * VoxelWorld._blockSize)) + vertOffset,});

            Vector3Int normal = dir.DirectionToVec();

            mesh._normals.AddRange(new Vector3[] { normal, normal, normal, normal });

            mesh._triangles.AddRange(new int[] {
                startIndex + 0,
                startIndex + 1,
                startIndex + 2,
                startIndex + 3,
                startIndex + 2,
                startIndex + 1,});
        }

        #endregion Mesh generation
    }
}
