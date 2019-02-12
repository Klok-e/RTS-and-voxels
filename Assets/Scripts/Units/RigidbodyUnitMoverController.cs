using ScriptableObjects.Containers;
using Scripts.Pathfinding;
using Scripts.World;
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

        [SerializeField]
        private float _drag;

        [SerializeField]
        private float _yAxisDrag;

        [SerializeField]
        private float _rotationSmoothness;

        private Transform _transform;
        private Rigidbody _rigidbody;

        private Dictionary<uint, bool> _coroutinesCanceled;

        private uint _coroutineId;

        private void Start()
        {
            _coroutinesCanceled = new Dictionary<uint, bool>();
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

            //drag
            var v = _rigidbody.velocity;
            _rigidbody.AddForce(new Vector3(v.x, Mathf.Sign(v.y) * _yAxisDrag, v.z) * -1f * _drag * Time.deltaTime, ForceMode.Impulse);

            //gravity
            _rigidbody.AddForce(new Vector3(0, -1, 0) * _gravity * Time.deltaTime, ForceMode.Impulse);
        }

        public override void MoveTo(Vector3 pos, Vector3 nextPos, Action onMoveComplete)
        {
            _coroutinesCanceled.Add(_coroutineId, false);
            StartCoroutine(MoveCoroutine(pos, nextPos, onMoveComplete, _coroutineId++));
        }

        public override void CancelMovement()
        {
            var keys = _coroutinesCanceled.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                _coroutinesCanceled[keys[i]] = true;
            }
        }

        private IEnumerator MoveCoroutine(Vector3 pos, Vector3 nextPos, Action onMoveComplete, uint id)
        {
            pos += new Vector3(0, _distanceToFloatAboveGround - VoxConsts._blockSize, 0);
            nextPos += new Vector3(0, _distanceToFloatAboveGround - VoxConsts._blockSize, 0);

            var errSqr = _allowedError * _allowedError;
            while ((pos - _rigidbody.position).sqrMagnitude > errSqr)
            {
                var euler = Quaternion.LookRotation(pos - _rigidbody.position).eulerAngles;
                _rigidbody.MoveRotation(Quaternion.Euler(0, Mathf.Lerp(_rigidbody.rotation.eulerAngles.y, euler.y, _rotationSmoothness), 0));
                //Debug.Log($"rigidbody pos: {_rigidbody.position}; pos: {pos}");
                _rigidbody.AddForce(((pos - _rigidbody.position).normalized * _speed.Value * Time.deltaTime), ForceMode.Impulse);
                if (_coroutinesCanceled[id])
                    break;

                yield return new WaitForFixedUpdate();
            }
            if (!_coroutinesCanceled[id])
                onMoveComplete();
            else
                _coroutinesCanceled.Remove(id);
        }
    }
}
