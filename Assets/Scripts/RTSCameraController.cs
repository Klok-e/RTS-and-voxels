using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [SerializeField]
    private float _speedRotation = 1;

    [SerializeField]
    private float _speedXZ = 1;

    [SerializeField]
    private float _speedY = 1;

    private void Start()
    {
        RenderSettings.fog = false;
    }

    private void Move()
    {
        float moveZ = Input.GetAxis("Vertical");
        float moveX = Input.GetAxis("Horizontal");

        float moveY = Input.GetAxis("Mouse ScrollWheel");

        var posBefore = transform.position;
        transform.Translate(new Vector3(moveX, 0, moveZ) * _speedXZ);
        var posAfter = transform.position;
        transform.position =  new Vector3(posAfter.x, posBefore.y, posAfter.z);
        transform.position += new Vector3(0, moveY, 0) * _speedY;
    }

    private void Rotate()
    {
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            var rotBy = new Vector3(mouseY, -mouseX, 0) * _speedRotation;

            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + rotBy);
        }
    }

    private void LateUpdate()
    {
        Move();
        Rotate();
    }
}