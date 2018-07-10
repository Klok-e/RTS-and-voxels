using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Pathfinding
{
    [DisallowMultipleComponent]
    public abstract class IMover : MonoBehaviour
    {
        public abstract void MoveTo(Vector3 pos, Vector3 nextPos, Action onMoveComplete);

        public abstract void MoveTo(Vector3 pos, Vector3 nextPos);
    }
}
