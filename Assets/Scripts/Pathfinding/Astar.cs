﻿using UnityEngine;

namespace Pathfinding
{
    public class Astar : MonoBehaviour
    {
        private static readonly Vector3Int[] neighbPosRelative =
        {
            new Vector3Int(-1, 1, 0), //up
            new Vector3Int(1,  1, 0),
            new Vector3Int(0,  1, 1),
            new Vector3Int(0,  1, -1),
            new Vector3Int(0,  1, 0),

            new Vector3Int(-1, -1, 0), //down
            new Vector3Int(1,  -1, 0),
            new Vector3Int(0,  -1, 1),
            new Vector3Int(0,  -1, -1),
            new Vector3Int(0,  -1, 0),

            new Vector3Int(-1, 0, 1), //left
            new Vector3Int(-1, 0, -1),
            new Vector3Int(-1, 0, 0),

            new Vector3Int(1, 0, 1), //right
            new Vector3Int(1, 0, -1),
            new Vector3Int(1, 0, 0),

            new Vector3Int(0, 0, 1), //front

            new Vector3Int(0, 0, -1), //back

            //corners
            //up
            new Vector3Int(-1, 1, 1),
            new Vector3Int(-1, 1, -1),
            new Vector3Int(1,  1, 1),
            new Vector3Int(1,  1, -1),

            //down
            new Vector3Int(-1, -1, 1),
            new Vector3Int(-1, -1, -1),
            new Vector3Int(1,  -1, 1),
            new Vector3Int(1,  -1, -1)
        };

        //[SerializeField]
        //private VoxelWorld _voxelWorld;
        /*
        private PathCellPool _pathCellPool;

        public void Start()
        {
            _pathCellPool = new PathCellPool();
        }
        
        public Vector3[] ConstructPath(Vector3 start, Vector3 destination)
        {
            var startInt = VoxelWorld.WorldPosToVoxelPos(start);
            var destinationInt = VoxelWorld.WorldPosToVoxelPos(destination);

            if (startInt == destinationInt)
                return null;

            if (!_voxelWorld.IsVoxelInBordersOfTheMap(startInt))
                throw new Exception();
            if (!_voxelWorld.IsVoxelInBordersOfTheMap(destinationInt))
                throw new Exception();

            var closedSet = new HashSet<PathCell>();
            var openSet = new Heap<PathCell>(100);

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
                if (z > 10000)
                {
                    break;
                }

                var current = openSet.RemoveFirst();
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
                    if ((current._gCost + DistanceManhattan(neihboursArr[i].Pos, current.Pos)) < neihboursArr[i]._gCost
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
                path.Add(destination);
                dest = dest.Parent;
                while (true)
                {
                    if (dest.Parent == null)
                        break;
                    path.Add(VoxelWorld.VoxelPosToWorldPos(dest.Pos));
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

                var down = new Vector3Int(0, -1, 0);

                for (int i = 0; i < neighbPosRelative.Length; i++)
                {
                    var dir = neighbPosRelative[i];
                    var pos = dir + pathCell.Pos;
                    if (_voxelWorld.IsVoxelInBordersOfTheMap(pos) && _voxelWorld.IsVoxelInBordersOfTheMap(pos + down))
                    {
                        if (_voxelWorld.GetVoxel(pos).type.IsAir())
                        {
                            if (!_voxelWorld.GetVoxel(pos + down).type.IsAir()//voxel lower than this vox must be ground
                                ||
                                dir == down)//or if dir is down (simulate falling)
                            {
                                var cell = _pathCellPool.GetPathCell(pos);
                                cell._hCost = DistanceManhattan(pos, destinationInt);
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
            }
        }

        /// <summary>
        /// Slow but accurate
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private static float DistanceEuclidean(Vector3Int from, Vector3Int to)
        {
            return (to - from).magnitude;
        }

        /// <summary>
        /// Super fast but inaccurate
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private static float DistanceManhattan(Vector3Int from, Vector3Int to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y) + Mathf.Abs(from.z - to.z);
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
                    mainPool.Enqueue(t);
                }
            }
        }*/
    }
}