using System;
using Help.DataContainers;
using UnityEngine;

namespace Pathfinding
{
    public class PathCell : IComparable<PathCell>, IHeapItem<PathCell>
    {
        public float _gCost;
        public float _hCost;

        public PathCell(Vector3Int pos)
        {
            Pos    = pos;
            _gCost = 0;
            _hCost = 0;
            Parent = null;
        }

        public Vector3Int Pos    { get; private set; }
        public PathCell   Parent { get; private set; }

        public float FCost => _gCost + _hCost;

        public int CompareTo(PathCell other)
        {
            return other.FCost.CompareTo(FCost);
        }

        public int HeapIndex { get; set; }

        public void SetPos(Vector3Int pos)
        {
            Pos = pos;
        }

        public void SetParent(PathCell parentNew)
        {
            Parent = parentNew;
            _gCost = parentNew._gCost + Vector3Int.Distance(parentNew.Pos, Pos);
        }

        public override int GetHashCode()
        {
            return Pos.GetHashCode();
        }
    }
}