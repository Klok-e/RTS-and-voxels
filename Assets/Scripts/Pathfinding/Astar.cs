using Scripts.Help;
using Scripts.World;
using System;
using System.Collections.Generic;
using System.Linq;
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
        };

        public Vector3[] GetPath(Vector3 start, Vector3 destination)
        {
            var startInt = NearestVoxelPos(start);
            var destinationInt = NearestVoxelPos(destination);

            var closedSet = new HashSet<PathCell>();
            var openSet = new List<PathCell>(1);

            openSet.Add(new PathCell(startInt)
            {
                _gCost = 0,
                _hCost = Vector3Int.Distance(startInt, destinationInt),
            });

            PathCell dest = null;
            while (openSet.Count > 0)
            {
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

                var neighb = GetNeighbours(current);
                for (int i = 0; i < neighb.Length; i++)
                {
                    neighb[i]._hCost = Distance(neighb[i].Pos, destinationInt);

                    if ((current._gCost + Distance(neighb[i].Pos, current.Pos)) < neighb[i]._gCost
                        ||
                        !openSet.Contains(neighb[i]))
                    {
                        neighb[i].SetParent(current);

                        if (!openSet.Contains(neighb[i]))
                            openSet.Add(neighb[i]);
                    }
                }
            }
            if (dest != null)
            {
                var path = new List<Vector3>();
                while (true)
                {
                    if (dest == null)
                        break;
                    path.Add(dest.Pos);
                    dest = dest.Parent;
                }
                path.Reverse();
                return path.ToArray();
            }
            else
            {
                return null;
            }

            PathCell[] GetNeighbours(PathCell pathCell)
            {
                var world = VoxelWorldController.Instance;
                var neighbours = new List<PathCell>(18);

                for (int i = 0; i < neighbPosRelative.Length; i++)
                {
                    var pos = neighbPosRelative[i] + pathCell.Pos;
                    if (world.IsVoxelInBordersOfTheMap(pos) && world.GetVoxel(pos).type.IsAir())
                    {
                        var cell = new PathCell(pos);
                        if (!closedSet.Contains(cell))
                        {
                            for (int k = 0; k < openSet.Count; k++)
                            {
                                if (openSet[k].Pos == cell.Pos)
                                {
                                    cell = openSet[k];
                                }
                            }
                            neighbours.Add(cell);
                        }
                    }
                }
                return neighbours.ToArray();
            }
        }

        private static Vector3Int NearestVoxelPos(Vector3 pos)
        {
            return (pos / VoxelWorldController._blockSize).ToInt();
        }

        private static float Distance(Vector3Int from, Vector3Int to)
        {
            return (to - from).magnitude;
        }
    }
}
