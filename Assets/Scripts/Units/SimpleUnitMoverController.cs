using Scripts.Help.ScriptableObjects.Containers;
using Scripts.Pathfinding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Units
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleUnitMoverController : IMover
    {
        [SerializeField]
        private FloatReference _speed;

        private Transform _transform;
        private Rigidbody _rigidbody;

        private void Start()
        {
            _transform = transform;
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void MoveTo(Vector3 pos, Vector3 nextPos, Action onMoveComplete)
        {
            StartCoroutine(SimpleMoveCoroutine(pos, 0.1f, onMoveComplete));
        }

        public override void MoveTo(Vector3 pos, Vector3 nextPos)
        {
            throw new NotImplementedException();
        }

        private IEnumerator SimpleMoveCoroutine(Vector3 destination, float allowedError, Action onMoveComplete)
        {
            var errSqr = allowedError * allowedError;
            while ((_transform.position - destination).sqrMagnitude > errSqr)
            {
                _rigidbody.MovePosition(_transform.position + ((destination - _transform.position).normalized * _speed.Value));
                yield return new WaitForFixedUpdate();
            }
            onMoveComplete();
        }
    }
}
