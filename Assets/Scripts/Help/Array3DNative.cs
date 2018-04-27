﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;

namespace Scripts.Help
{
    public struct Array3DNative<T> : IDisposable
           where T : struct
    {
        private int _xMax;
        private int _yMax;
        private int _zMax;

        private NativeArray<T> _arr;

        public Array3DNative(int xMax, int yMax, int zMax, Allocator allocator)
        {
            _xMax = xMax;
            _yMax = yMax;
            _zMax = zMax;
            _arr = new NativeArray<T>(xMax * yMax * zMax, allocator);
        }

        public void Dispose()
        {
            _arr.Dispose();
        }

        public void At(int i, out int x, out int y, out int z)
        {
            x = i % _xMax;
            y = (i / _xMax) % _yMax;
            z = i / (_xMax * _yMax);
        }

        public T this[int i]
        {
            //(z * xMax * yMax) + (y * xMax) + x;
            get { return _arr[i]; }
            set { _arr[i] = value; }
        }

        public T this[int x, int y, int z]
        {
            //(z * xMax * yMax) + (y * xMax) + x;
            get { return _arr[z * _xMax * _yMax + y * _xMax + x]; }
            set { _arr[z * _xMax * _yMax + y * _xMax + x] = value; }
        }
    }
}
