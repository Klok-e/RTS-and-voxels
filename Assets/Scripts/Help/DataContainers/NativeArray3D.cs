using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Scripts.Help.DataContainers
{
    public struct NativeArray3D<T> : IDisposable
           where T : struct
    {
        public int XMax { get; }
        public int YMax { get; }
        public int ZMax { get; }

        public bool IsCreated { get { return _arr.IsCreated; } }

        private NativeArray<T> _arr;

        public NativeArray3D(int xMax, int yMax, int zMax, Allocator allocator, NativeArrayOptions nativeArrayOptions = NativeArrayOptions.ClearMemory)
        {
            XMax = xMax;
            YMax = yMax;
            ZMax = zMax;
            _arr = new NativeArray<T>(xMax * yMax * zMax, allocator, nativeArrayOptions);
        }

        public NativeArray3D(NativeArray3D<T> toCopy, Allocator allocator)
        {
            XMax = toCopy.XMax;
            YMax = toCopy.YMax;
            ZMax = toCopy.ZMax;
            _arr = new NativeArray<T>(toCopy._arr, allocator);
        }

        public void CopyFrom(NativeArray3D<T> toCopy)
        {
#if UNITY_EDITOR
            if (toCopy.XMax != XMax || toCopy.YMax != YMax || toCopy.ZMax != ZMax)
            {
                throw new InvalidOperationException("sizes don't match");
            }
#endif
            _arr.CopyFrom(toCopy._arr);
        }

        public void Dispose()
        {
            _arr.Dispose();
        }

        public void At(int i, out int x, out int y, out int z)
        {
            x = i % XMax;
            y = (i / XMax) % YMax;
            z = i / (XMax * YMax);
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
            get { return _arr[z * XMax * YMax + y * XMax + x]; }
            set { _arr[z * XMax * YMax + y * XMax + x] = value; }
        }
    }
}
