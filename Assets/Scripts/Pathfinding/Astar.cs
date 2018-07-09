using Scripts.Help;
using Scripts.World;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Pathfinding
{
    [RequireComponent(typeof(VoxelWorldController))]
    public class Astar : MonoBehaviour
    {
        private static readonly Vector3Int[] neighbPosRelative = new Vector3Int[]
        {
                new Vector3Int(-1, 1, 0),//up
                new Vector3Int(1, 1, 0),
                new Vector3Int(0, 1, 1),
                new Vector3Int(0, 1, -1),
                new Vector3Int(0, 1, 0),

                new Vector3Int(-1, -1, 0),//down
                new Vector3Int(1, -1, 0),
                new Vector3Int(0, -1, 1),
                new Vector3Int(0, -1, -1),
                new Vector3Int(0, -1, 0),

                new Vector3Int(-1, 0, 1),//left
                new Vector3Int(-1, 0, -1),
                new Vector3Int(-1, 0, 0),

                new Vector3Int(1, 0, 1),//right
                new Vector3Int(1, 0, -1),
                new Vector3Int(1, 0, 0),

                new Vector3Int(0, 0, 1),//front

                new Vector3Int(0, 0, -1),//back

                //corners
                //up
                new Vector3Int(-1, 1, 1),
                new Vector3Int(-1, 1, -1),
                new Vector3Int(1, 1, 1),
                new Vector3Int(1, 1, -1),

                //down
                new Vector3Int(-1, -1, 1),
                new Vector3Int(-1, -1, -1),
                new Vector3Int(1, -1, 1),
                new Vector3Int(1, -1, -1),
        };

        private PathCellPool _pathCellPool;

        private void Start()
        {
            _pathCellPool = new PathCellPool();
        }

        public Vector3[] ConstructPath(Vector3 start, Vector3 destination)
        {
            var startInt = WorldPosToVoxelPos(start);
            var destinationInt = WorldPosToVoxelPos(destination);

            var closedSet = new HashSet<PathCell>();
            var openSet = new List<PathCell>(100);

            var cells = new Dictionary<Vector3Int, PathCell>(100);

            var neihboursArr = new PathCell[neighbPosRelative.Length];

            var t = new PathCell(startInt)
            {
                _gCost = 0,
                _hCost = Vector3Int.Distance(startInt, destinationInt),
            };

            openSet.Add(t);
            cells.Add(t.Pos, t);

            int z = 0;

            PathCell dest = null;
            while (openSet.Count > 0)
            {
                z++;
                if (z > 1000)
                {
                    break;
                }

                int ind = 0;
                var current = openSet[ind];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].FCost < current.FCost)
                    {
                        ind = i;
                        current = openSet[ind];
                    }
                }
                openSet.RemoveAt(ind);
                closedSet.Add(current);

                if (current.Pos == destinationInt)
                {
                    dest = current;
                    break;
                }

                GetNeighboursNoAlloc(current, neihboursArr);
                for (int i = 0; i < neihboursArr.Length; i++)
                {
                    if (neihboursArr[i] == null)
                        continue;

                    var contains = openSet.Contains(neihboursArr[i]);
                    if ((current._gCost + Distance(neihboursArr[i].Pos, current.Pos)) < neihboursArr[i]._gCost
                        ||
                        !contains)
                    {
                        neihboursArr[i].SetParent(current);

                        if (!contains)
                            openSet.Add(neihboursArr[i]);
                    }
                }
            }
            _pathCellPool.PoolAllCreated();
            if (dest != null)
            {
                var path = new List<Vector3>();
                while (true)
                {
                    if (dest.Parent == null)
                        break;
                    path.Add(VoxelPosToWorldPos(dest.Pos));
                    dest = dest.Parent;
                }
                path.Reverse();
                return path.ToArray();
            }
            else
            {
                return null;
            }

            void GetNeighboursNoAlloc(PathCell pathCell, PathCell[] bufferArray)
            {
                for (int i = 0; i < bufferArray.Length; i++)//reset array
                    bufferArray[i] = null;

                var world = VoxelWorldController.Instance;

                for (int i = 0; i < neighbPosRelative.Length; i++)
                {
                    var pos = neighbPosRelative[i] + pathCell.Pos;
                    if (world.IsVoxelInBordersOfTheMap(pos) && world.GetVoxel(pos).type.IsAir())
                    {
                        var cell = _pathCellPool.GetPathCell(pos);
                        cell._hCost = Distance(pos, destinationInt);
                        if (cells.ContainsKey(pos))
                        {
                            cell = cells[pos];
                        }
                        else
                        {
                            cells.Add(pos, cell);
                        }

                        if (!closedSet.Contains(cell))
                        {
                            bufferArray[i] = cell;
                        }
                    }
                }
            }
        }

        private static Vector3Int WorldPosToVoxelPos(Vector3 pos)
        {
            return (pos / VoxelWorldController._blockSize).ToInt();
        }

        private static Vector3 VoxelPosToWorldPos(Vector3Int pos)
        {
            return ((Vector3)pos * VoxelWorldController._blockSize);
        }

        private static float Distance(Vector3Int from, Vector3Int to)
        {
            return (to - from).magnitude;
        }

        private class PathCellPool
        {
            private Queue<PathCell> mainPool;
            private Queue<PathCell> toPoolLater;

            public PathCellPool()
            {
                mainPool = new Queue<PathCell>();
                toPoolLater = new Queue<PathCell>();
            }

            public PathCell GetPathCell(Vector3Int pos)
            {
                PathCell c;
                if (mainPool.Count > 0)
                {
                    c = mainPool.Dequeue();
                    c.SetPos(pos);
                }
                else
                {
                    c = new PathCell(pos);
                }
                toPoolLater.Enqueue(c);
                return c;
            }

            public void PoolAllCreated()
            {
                while (toPoolLater.Count > 0)
                {
                    var t = toPoolLater.Dequeue();
                    t.Deinitialize();
                    mainPool.Enqueue(t);
                }
            }
        }
    }
}
