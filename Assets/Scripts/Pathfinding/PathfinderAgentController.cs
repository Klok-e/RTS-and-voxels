using Scripts.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Pathfinding
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IMover))]
    public class PathfinderAgentController : MonoBehaviour
    {
        [SerializeField]
        private Astar _astar;

        private Transform _trasform;

        private IMover _mover;

        private Vector3[] _pathToDraw;

        private bool _isMoving;

        private void Start()
        {
            _trasform = transform;
            _mover = GetComponent<IMover>();
        }

        public void MoveTo(Vector3 destination)
        {
            var start = VoxelCastRayDown(_trasform.position, 10);
            if (start.DidHit)
            {
                var dest = VoxelCastRayDown(destination, 10);
                if (dest.DidHit)
                {
                    if (_isMoving)
                    {
                        _mover.CancelMovement();
                    }

                    var path = ConstructPath(new Vector3(_trasform.position.x, start.Pos.y + VoxelWorldController._blockSize, _trasform.position.z),
                                             new Vector3(destination.x, dest.Pos.y + VoxelWorldController._blockSize, destination.z));
                    if (path != null)
                    {
                        if (path.Length >= 2)
                        {
                            int i = 0;
                            void CallMoveToIfPathNotFinishedYet()
                            {
                                if (i >= path.Length - 1)
                                {
                                    if (i % 2 == 0) i--;
                                    _mover.MoveTo(path[i], path[i], FinishMoving);
                                    return;
                                }
                                _mover.MoveTo(path[i++], path[i++], CallMoveToIfPathNotFinishedYet);
                            }
                            _mover.MoveTo(path[i++], path[i++], CallMoveToIfPathNotFinishedYet);
                        }
                        else
                        {
                            _mover.MoveTo(path[0], path[0], FinishMoving);
                        }
                        _isMoving = true;
                    }
                }
            }
            void FinishMoving()
            {
                _isMoving = false;
            }
        }

        private Vector3[] ConstructPath(Vector3 start, Vector3 stop)
        {
            //var w = new Stopwatch();
            //w.Start();
            var path = _astar.ConstructPath(start, stop);
            //w.Stop();
            //UnityEngine.Debug.Log($"Time spent on constructing path (ms): {w.ElapsedMilliseconds}");
            _pathToDraw = path;
            return path;
        }

        private struct VoxelRaycastHit
        {
            public Vector3 Pos { get; }
            public bool DidHit { get; }

            public VoxelRaycastHit(Vector3 pos, bool didHit)
            {
                Pos = pos;
                DidHit = didHit;
            }
        }

        private VoxelRaycastHit VoxelCastRayDown(Vector3 start, int distance)
        {
            var down = new Vector3Int(0, -1, 0);

            var world = VoxelWorldController.Instance;
            var current = VoxelWorldController.WorldPosToVoxelPos(start);

            bool hit = false;
            var voxelsPassed = 0;
            while (voxelsPassed < distance)
            {
                if (!world.GetVoxel(current).type.IsAir())
                {
                    hit = true;
                    break;
                }
                current += down;
                voxelsPassed += 1;
            }
            return new VoxelRaycastHit(VoxelWorldController.VoxelPosToWorldPos(current), hit);
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (_pathToDraw != null)
            {
                for (int i = 0; i < _pathToDraw.Length - 1; i++)
                {
                    Gizmos.DrawLine(_pathToDraw[i], _pathToDraw[i + 1]);
                }
            }
#endif
        }
    }
}
