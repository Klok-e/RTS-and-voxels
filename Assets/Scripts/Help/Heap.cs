using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Scripts.Help
{
    public class Heap<T> where T : IHeapItem<T>
    {
        private List<T> _items;

        public Heap(int size)
        {
            _items = new List<T>(size);
        }

        public void Add(T item)
        {
            item.HeapIndex = Count;
            _items.Add(item);
            SortUp(item);
        }

        public T RemoveFirst()
        {
            T firstItem = _items[0];
            _items[0] = _items[Count - 1];
            _items[0].HeapIndex = 0;
            SortDown(_items[0]);
            _items.RemoveAt(Count - 1);
            return firstItem;
        }

        public void UpdateItem(T item)
        {
            SortUp(item);
        }

        public int Count
        {
            get
            {
                return _items.Count;
            }
        }

        public bool Contains(T item)
        {
            if (item.HeapIndex < Count)
                return _items[item.HeapIndex].Equals(item);
            else
                return false;
        }

        private void SortDown(T item)
        {
            while (true)
            {
                int childIndexLeft = item.HeapIndex * 2 + 1;
                int childIndexRight = item.HeapIndex * 2 + 2;
                int swapIndex;

                if (childIndexLeft < Count)
                {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < Count)
                    {
                        if (_items[childIndexLeft].CompareTo(_items[childIndexRight]) < 0)
                        {
                            swapIndex = childIndexRight;
                        }
                    }

                    if (item.CompareTo(_items[swapIndex]) < 0)
                    {
                        Swap(item, _items[swapIndex]);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private void SortUp(T item)
        {
            int parentIndex = (item.HeapIndex - 1) / 2;

            while (true)
            {
                T parentItem = _items[parentIndex];
                if (item.CompareTo(parentItem) > 0)
                {
                    Swap(item, parentItem);
                }
                else
                {
                    break;
                }

                parentIndex = (item.HeapIndex - 1) / 2;
            }
        }

        private void Swap(T itemA, T itemB)
        {
            _items[itemA.HeapIndex] = itemB;
            _items[itemB.HeapIndex] = itemA;
            int itemAIndex = itemA.HeapIndex;
            itemA.HeapIndex = itemB.HeapIndex;
            itemB.HeapIndex = itemAIndex;
        }
    }

    public interface IHeapItem<T> : IComparable<T>
    {
        int HeapIndex
        {
            get;
            set;
        }
    }
}
