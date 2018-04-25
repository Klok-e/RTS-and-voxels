using UnityEngine;
using System.Collections.Generic;
using ProceduralNoiseProject;
using Scripts.Help;
using Unity.Collections;
using Scripts.World;
using System;

namespace MarchingCubesProject
{
    public enum MARCHING_MODE { CUBES, TETRAHEDRON };

    public class Example : MonoBehaviour
    {
        public Material m_material;

        public MARCHING_MODE mode = MARCHING_MODE.CUBES;

        public int seed = 0;

        //The size of voxel array.
        private int width = 16;

        private int height = 16;
        private int length = 16;

        private Mesh mesh;

        private Marching marching = null;

        private FractalNoise fractal;

        private void Start()
        {
            GameObject go = new GameObject("Chunk");
            go.transform.parent = transform;
            mesh = go.AddComponent<MeshFilter>().mesh;
            go.AddComponent<MeshRenderer>();
            go.GetComponent<Renderer>().material = m_material;

            INoise perlin = new PerlinNoise(seed, 2.0f);
            fractal = new FractalNoise(perlin, 3, 1.0f);
            fractal.Amplitude = 0.8f;
            fractal.UpdateTable();

            //Set the mode used to create the mesh.
            //Cubes is faster and creates less verts, tetrahedrons is slower and creates more verts but better represents the mesh surface.

            if (mode == MARCHING_MODE.TETRAHEDRON)
                marching = new MarchingTertrahedron();
            else
                marching = new MarchingCubes();

            //Surface is the value that represents the surface of mesh
            //For example the perlin noise has a range of -1 to 1 so the mid point is where we want the surface to cut through.
            //The target value does not have to be the mid point it can be any value with in the range.
            marching.Surface = 0f;

            Generate();
        }

        private Array3DNative<Voxel> CreateVoxels()
        {
            throw new NotImplementedException();
            var voxels = new Array3DNative<Voxel>(width, height, length, Allocator.Persistent);

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < length; z++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        float value = (x == 0) || (x == width - 1) || (y == 0) || (y == height - 1) || (z == 0) || (z == length - 1) ? 0 : 1;
                        //Array2D.SetValueIn3dArr(voxels, width, height, length, x, y, z, value);
                    }
                }
            }

            return voxels;
        }

        private float[] CreateVoxelsPerlin()
        {
            float[] voxels = new float[width * height * length];

            //Fill voxels with values. Im using perlin noise but any method to create voxels will work.
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < length; z++)
                    {
                        float fx = x / (width - 1.0f);
                        float fy = y / (height - 1.0f);
                        float fz = z / (length - 1.0f);

                        int idx = x + y * width + z * width * height;

                        voxels[idx] = fractal.Sample3D(fx, fy, fz);
                        //voxels[idx] = Random.Range(-0.5f, 2);
                        //voxels[idx] = y == 0 && y == height - 1 ? 0f : 1f;
                    }
                }
            }
            //fractal.Offset += Vector3.one * 0.001f;
            return voxels;
        }

        private Vector2[] CalculateUV(Vector3[] vertices)
        {
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < uv.Length - 3; i += 4)
            {
                uv[i] = new Vector2(0.0f, 0.0f);
                uv[i + 1] = new Vector2(0.333f, 0.0f);
                uv[i + 2] = new Vector2(0.0f, 0.333f);
                uv[i + 3] = new Vector2(0.333f, 0.333f);
            }
            return uv;
        }

        private Color[] CalculateColours(Vector3[] vertices)
        {
            var colours = new Color[vertices.Length];
            for (int i = 0; i < colours.Length; i += 1)
            {
                colours[i] = Color.Lerp(Color.red, Color.white, vertices[i].y / height);
            }
            return colours;
        }

        private void Generate()
        {
            var voxels = CreateVoxels();

            List<Vector3> verts = new List<Vector3>();
            List<int> indices = new List<int>();

            //The mesh produced is not optimal. There is one vert for each index.
            //Would need to weld vertices for better quality mesh.
            //marching.Generate(voxels, width, height, length, verts, indices);

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);
            mesh.uv = CalculateUV(mesh.vertices);

            //mesh.colors = CalculateColours(mesh.vertices);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals(60);
            mesh.RecalculateTangents();
        }

        private void Update()
        {
            //Generate();
        }
    }
}
