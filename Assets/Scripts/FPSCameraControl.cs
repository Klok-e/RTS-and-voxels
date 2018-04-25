using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts
{
    public class FPSCameraControl : MonoBehaviour
    {
        [SerializeField] private float minimumVert = -45.0f;
        [SerializeField] private float maximumVert = 45.0f;

        [SerializeField] private float sensHorizontal = 10.0f;
        [SerializeField] private float sensVertical = 10.0f;

        [SerializeField] private Plugins.ScriptableObjects.Containers.FloatReference moveSpeed;
        private float _rotationX = 0;

        private bool isInputCatchingNeeded = true;

        public void SetInputCatching(bool set)
        {
            isInputCatchingNeeded = set;
        }

        // Update is called once per frame
        private void Update()
        {
            if (isInputCatchingNeeded)
            {
                RotateX();
                RotateY();

                Move();
            }
        }

        private void Move()
        {
            var forward = Input.GetAxis("Vertical");
            var sideways = Input.GetAxis("Horizontal");

            transform.Translate(new Vector3(sideways, 0, forward).normalized * moveSpeed.Value * Time.deltaTime);
        }

        private void RotateY()
        {
            _rotationX -= Input.GetAxis("Mouse Y") * sensVertical;
            _rotationX = Mathf.Clamp(_rotationX, minimumVert, maximumVert); //Clamps the vertical angle within the min and max limits (45 degrees)

            float rotationY = transform.localEulerAngles.y;

            transform.localEulerAngles = new Vector3(_rotationX, rotationY, 0);
        }

        private void RotateX()
        {
            transform.Rotate(0, Input.GetAxis("Mouse X") * sensHorizontal, 0);
        }
    }
}
