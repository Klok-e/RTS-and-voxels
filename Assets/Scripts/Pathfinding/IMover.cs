using System;
using UnityEngine;

namespace Pathfinding
{
    [DisallowMultipleComponent]
    public abstract class IMover : MonoBehaviour
    {
        public abstract void CancelMovement();

        public abstract void MoveTo(Vector3 pos, Vector3 nextPos, Action onMoveComplete);
    }
}