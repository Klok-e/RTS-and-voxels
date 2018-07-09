using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Pathfinding
{
    public class PathfinderAgentController : MonoBehaviour
    {
        [SerializeField]
        private Astar _astar;

        [SerializeField]
        public Transform _destination;

        private Vector3[] _pathToDraw;

        private void Start()
        {
            //_lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        private void Update()
        {
            ConstructPath(transform.position, _destination.position);
        }

        public void ConstructPath(Vector3 start, Vector3 stop)
        {
            var w = new Stopwatch();
            w.Start();
            var path = _astar.ConstructPath(start, stop);
            w.Stop();
            UnityEngine.Debug.Log($"Time spent on constructing path (ms): {w.ElapsedMilliseconds}");
            _pathToDraw = path;
        }

        private void OnDrawGizmos()
        {
            if (_pathToDraw == null)
                return;

            for (int i = 0; i < _pathToDraw.Length - 1; i++)
            {
                Gizmos.DrawLine(_pathToDraw[i], _pathToDraw[i + 1]);
            }
        }
    }
}
