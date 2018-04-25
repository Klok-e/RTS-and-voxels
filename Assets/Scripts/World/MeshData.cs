using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace Scripts.World
{
    public class MeshData
    {
        public List<int> _triangles { get; }
        public List<Vector3> _vertices { get; }
        public List<Color32> _colors { get; }
        public List<Vector3> _normals { get; }

        public MeshData(List<Vector3> vertices, List<int> triangles, List<Color32> colors, List<Vector3> normals)
        {
            _triangles = new List<int>(triangles);
            _vertices = new List<Vector3>(vertices);
            _colors = new List<Color32>(colors);
            _normals = new List<Vector3>(normals);
        }
    }

    public struct MeshDataNative
    {
        public List<int> _triangles { get; }
        public List<Vector3> _vertices { get; }
        public List<Color32> _colors { get; }
        public List<Vector3> _normals { get; }

        public MeshData(List<Vector3> vertices, List<int> triangles, List<Color32> colors, List<Vector3> normals)
        {
            _triangles = new List<int>(triangles);
            _vertices = new List<Vector3>(vertices);
            _colors = new List<Color32>(colors);
            _normals = new List<Vector3>(normals);
        }
    }
}
