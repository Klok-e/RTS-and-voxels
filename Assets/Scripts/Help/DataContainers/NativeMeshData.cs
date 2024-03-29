﻿using System;
using Unity.Collections;
using UnityEngine;

namespace Help.DataContainers
{
    public struct NativeMeshData : IDisposable
    {
        public NativeList<int>     _triangles;
        public NativeList<Vector3> _vertices;
        public NativeList<Color>   _colors;
        public NativeList<Vector3> _normals;
        public NativeList<Vector2> _uv, _uv2, _uv3;

        public NativeMeshData(int size, Allocator allocator)
        {
            _triangles = new NativeList<int>(size, allocator);
            _vertices  = new NativeList<Vector3>(size, allocator);
            _colors    = new NativeList<Color>(size, allocator);
            _normals   = new NativeList<Vector3>(size, allocator);
            _uv        = new NativeList<Vector2>(size, allocator);
            _uv2       = new NativeList<Vector2>(size, allocator);
            _uv3       = new NativeList<Vector2>(size, allocator);
        }

        public void Clear()
        {
            _triangles.Clear();
            _vertices.Clear();
            _colors.Clear();
            _normals.Clear();
            _uv.Clear();
            _uv2.Clear();
            _uv3.Clear();
        }

        public void Dispose()
        {
            _triangles.Dispose();
            _vertices.Dispose();
            _colors.Dispose();
            _normals.Dispose();
            _uv.Dispose();
            _uv2.Dispose();
            _uv3.Dispose();
        }
    }
}