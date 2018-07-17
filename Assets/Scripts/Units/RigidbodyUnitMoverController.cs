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
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyUnitMoverController : IMover
    {
        [SerializeField]
        private FloatReference _speed;

        [SerializeField]
        private float _allowedError;

        [SerializeField]
        private float _distanceToFloatAboveGround;

        [SerializeField]
        private float _gravity;

        [SerializeField]
        private float _floatForce;

        private Transform _transform;
        private Rigidbody _rigidbody;

        private bool _isCanceled;

        private void Start()
        {
            _transform = transform;
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            var downRay = new Ray(_rigidbody.position, new Vector3(0, -1, 0));
            if (Physics.Raycast(downRay, out var hit, _distanceToFloatAboveGround))
            {
                _rigidbody.AddForce(new Vector3(0, 1, 0) * (_distanceToFloatAboveGround - hit.distance) * _floatForce * Time.deltaTime, ForceMode.Impulse);
            }

            //gravity
            _rigidbody.AddForce(new Vector3(0, -1, 0) * _gravity * Time.deltaTime, ForceMode.Impulse);
        }

        public override void MoveTo(Vector3 pos, Vector3 nextPos, Action onMoveComplete)
        {
            _isCanceled = false;
            StartCoroutine(MoveCoroutine(pos, nextPos, onMoveComplete));
        }

        public override void CancelMovement()
        {
            _isCanceled = true;
        }

        private IEnumerator MoveCoroutine(Vector3 pos, Vector3 nextPos, Action onMoveComplete)
        {
            var errSqr = _allowedError * _allowedError;

            while ((new Vector3(pos.x, 0, pos.z) - new Vector3(_rigidbody.position.x, 0, _rigidbody.position.z)).sqrMagnitude > errSqr)
            {
                _rigidbody.MovePosition(_rigidbody.position + ((new Vector3(pos.x, 0, pos.z) - new Vector3(_rigidbody.position.x, 0, _rigidbody.position.z)).normalized * _speed.Value * Time.deltaTime));
                if (_isCanceled)
                {
                    break;
                }
                yield return new WaitForFixedUpdate();
            }
            if (!_isCanceled)
                onMoveComplete();
        }
    }
}
