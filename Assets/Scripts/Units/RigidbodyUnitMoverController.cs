using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using ScriptableObjects.Containers;
using UnityEngine;
using World;

namespace Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyUnitMoverController : IMover
    {
        [SerializeField]
        private float _allowedError;

        private uint _coroutineId;

        private Dictionary<uint, bool> _coroutinesCanceled;

        [SerializeField]
        private float _distanceToFloatAboveGround;

        [SerializeField]
        private float _drag;

        [SerializeField]
        private float _floatForce;

        [SerializeField]
        private float _gravity;

        private Rigidbody _rigidbody;

        [SerializeField]
        private float _rotationSmoothness;

        [SerializeField]
        private FloatReference _speed;

        private Transform _transform;

        [SerializeField]
        private float _yAxisDrag;

        private void Start()
        {
            _coroutinesCanceled = new Dictionary<uint, bool>();
            _transform          = transform;
            _rigidbody          = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            var downRay = new Ray(_rigidbody.position, new Vector3(0, -1, 0));
            if (Physics.Raycast(downRay, out var hit, _distanceToFloatAboveGround))
                _rigidbody.AddForce(
                    new Vector3(0, 1, 0) * (_distanceToFloatAboveGround - hit.distance) * _floatForce * Time.deltaTime,
                    ForceMode.Impulse);

            //drag
            var v = _rigidbody.velocity;
            _rigidbody.AddForce(new Vector3(v.x, Mathf.Sign(v.y) * _yAxisDrag, v.z) * -1f * _drag * Time.deltaTime,
                ForceMode.Impulse);

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
            var keys                                                           = _coroutinesCanceled.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++) _coroutinesCanceled[keys[i]] = true;
        }

        private IEnumerator MoveCoroutine(Vector3 pos, Vector3 nextPos, Action onMoveComplete, uint id)
        {
            pos     += new Vector3(0, _distanceToFloatAboveGround - VoxConsts._blockSize, 0);
            nextPos += new Vector3(0, _distanceToFloatAboveGround - VoxConsts._blockSize, 0);

            float errSqr = _allowedError * _allowedError;
            while ((pos - _rigidbody.position).sqrMagnitude > errSqr)
            {
                var euler = Quaternion.LookRotation(pos - _rigidbody.position).eulerAngles;
                _rigidbody.MoveRotation(Quaternion.Euler(0,
                    Mathf.Lerp(_rigidbody.rotation.eulerAngles.y, euler.y, _rotationSmoothness), 0));
                //Debug.Log($"rigidbody pos: {_rigidbody.position}; pos: {pos}");
                _rigidbody.AddForce((pos - _rigidbody.position).normalized * _speed.Value * Time.deltaTime,
                    ForceMode.Impulse);
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