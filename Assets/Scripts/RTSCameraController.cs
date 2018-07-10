using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts
{
    public class RTSCameraController : MonoBehaviour
    {
        [SerializeField]
        private float _speedXZ = 1;

        [SerializeField]
        private float _speedY = 1;

        [SerializeField]
        private float _speedRotation = 1;

        private void Start()
        {
        }

        private void Move()
        {
            var moveZ = Input.GetAxis("Vertical") * _speedXZ;
            var moveX = Input.GetAxis("Horizontal") * _speedXZ;

            var moveY = Input.GetAxis("Mouse ScrollWheel") * _speedY;

            transform.Translate(new Vector3(moveX, moveY, moveZ));
        }

        private void Rotate()
        {
            if (Input.GetMouseButton(2))
            {
                var mouseX = Input.GetAxis("Mouse X");
                var mouseY = Input.GetAxis("Mouse Y");

                transform.Rotate(new Vector3(mouseX, mouseY, 0) * _speedRotation);
            }
        }

        private void LateUpdate()
        {
            Move();
            Rotate();
        }
    }
}
