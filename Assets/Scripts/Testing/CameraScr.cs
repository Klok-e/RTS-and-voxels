using UnityEngine;

namespace MarchingCubesProject
{
    public class CameraScr : MonoBehaviour
    {
        public float rotationSpeed = 1f;
        public float moveSpeed = 3.5f;
        private float X;
        private float Y;

        private void Start()
        {
            RenderSettings.fog = false;
        }

        private void LateUpdate()
        {
            transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * rotationSpeed, Input.GetAxis("Mouse X") * rotationSpeed, 0));
            X = transform.rotation.eulerAngles.x;
            Y = transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Euler(X, Y, 0);

            var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * moveSpeed;
            transform.Translate(move);
        }
    }
}
