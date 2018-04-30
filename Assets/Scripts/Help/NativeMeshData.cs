using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace Scripts.Help
{
    public struct NativeMeshData : IDisposable
    {
        public readonly NativeList<int> _triangles;
        public readonly NativeList<Vector3> _vertices;
        public readonly NativeList<Color32> _colors;
        public readonly NativeList<Vector3> _normals;

        public NativeMeshData(int size, Allocator allocator)
        {
            _triangles = new NativeList<int>(size, allocator);
            _vertices = new NativeList<Vector3>(size, allocator);
            _colors = new NativeList<Color32>(size, allocator);
            _normals = new NativeList<Vector3>(size, allocator);
        }

        public void Clear()
        {
            _triangles.Clear();
            _vertices.Clear();
            _colors.Clear();
            _normals.Clear();
        }

        public void Dispose()
        {
            _triangles.Dispose();
            _vertices.Dispose();
            _colors.Dispose();
            _normals.Dispose();
        }
    }
}
