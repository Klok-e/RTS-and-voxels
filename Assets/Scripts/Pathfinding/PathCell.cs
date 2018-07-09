using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Pathfinding
{
    public class PathCell : IEquatable<PathCell>
    {
        public Vector3Int Pos { get; private set; }
        public PathCell Parent { get; private set; }

        public float _gCost;
        public float _hCost;

        public float FCost
        {
            get => _gCost + _hCost;
        }

        public PathCell(Vector3Int pos)
        {
            Pos = pos;
            _gCost = 0;
            _hCost = 0;
            Parent = null;
        }

        public void Deinitialize()
        {
            Parent = null;
        }

        public void SetPos(Vector3Int pos)
        {
            Pos = pos;
        }

        public void SetParent(PathCell parentNew)
        {
            Parent = parentNew;
            _gCost = parentNew._gCost + Vector3Int.Distance(parentNew.Pos, Pos);
        }

        public bool Equals(PathCell obj)
        {
            return Pos.Equals(obj.Pos);
        }

        public override int GetHashCode()
        {
            return Pos.GetHashCode();
        }
    }
}
