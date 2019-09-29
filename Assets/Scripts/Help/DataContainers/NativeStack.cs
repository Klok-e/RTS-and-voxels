using System;
using Unity.Collections;

namespace Help.DataContainers
{
    public struct NativeStack<T> : IDisposable
        where T : struct
    {
        private NativeList<T> list;

        public int Length => list.Length;

        public NativeStack(int size, Allocator allocator)
        {
            list = new NativeList<T>(size, allocator);
        }

        public void Push(T item)
        {
            list.Add(item);
        }

        public T Pop()
        {
            var item = list[list.Length - 1];
            list.RemoveAtSwapBack(list.Length - 1);
            return item;
        }

        public void Dispose()
        {
            list.Dispose();
        }
    }
}