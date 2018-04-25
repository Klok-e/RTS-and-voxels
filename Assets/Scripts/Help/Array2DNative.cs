using UnityEngine;
using System.Collections;
using Unity.Collections;

namespace Scripts.Help
{
    public struct Array2DNative<T>
        where T : struct
    {
        private int _rows;
        private int _columns;

        private NativeArray<T> _arr;

        public Array2DNative(int d1, int d2, Allocator allocator)
        {
            _rows = d1;
            _columns = d2;
            _arr = new NativeArray<T>(d1 * d2, allocator);
        }

        public T this[int x, int y]
        {
            get { return _arr[x * _columns + y]; }
            set { _arr[x * _columns + y] = value; }
        }
    }
}
