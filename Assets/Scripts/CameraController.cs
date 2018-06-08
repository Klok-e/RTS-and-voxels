using Scripts;
using Scripts.Help;
using Scripts.World;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace MarchingCubesProject
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField]
        private float rotationSpeed = 1f;

        [SerializeField]
        private float moveSpeed = 3.5f;

        private float X;
        private float Y;

        [SerializeField]
        private bool _isBlockInsertion;

        [SerializeField]
        private bool _isClearing;

        [SerializeField]
        private bool _isLightInsertion;

        [SerializeField]
        private int _sphereSize = 3;

        private void Start()
        {
            RenderSettings.fog = false;
        }

        public void SetClearing(bool set)
        {
            _isClearing = set;
        }

        public void SetBlockInsertion(bool set)
        {
            _isBlockInsertion = set;
        }

        public void SetLightInsertion(bool set)
        {
            _isLightInsertion = set;
        }

        public void SetSphereSize(float size)
        {
            _sphereSize = (int)size;
        }

        private void Update()
        {
            if (Input.GetMouseButton(0))
            {
                RaycastHit hit;
                Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit))
                {
                    var chunk = hit.collider.GetComponent<RegularChunk>();
                    if (chunk)
                    {
                        var pos = hit.point + (hit.normal * (VoxelWorldController._blockSize * (_isClearing ? -1 : 1)) / 2);
                        if (_isLightInsertion)
                        {
                            VoxelWorldController.Instance.SetLight(pos / VoxelWorldController._blockSize, 31);
                        }
                        else if (_isBlockInsertion)
                        {
                            VoxelWorldController.Instance.SetVoxel(pos / VoxelWorldController._blockSize, _isClearing ? VoxelType.Air : VoxelType.Solid);
                        }
                        else
                        {
                            VoxelWorldController.Instance.InsertSphere(pos / VoxelWorldController._blockSize, _sphereSize, _isClearing ? VoxelType.Air : VoxelType.Solid);
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (Input.GetMouseButton(1))
            {
                transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * rotationSpeed, Input.GetAxis("Mouse X") * rotationSpeed, 0));
                X = transform.rotation.eulerAngles.x;
                Y = transform.rotation.eulerAngles.y;
                transform.rotation = Quaternion.Euler(X, Y, 0);
            }

            var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * moveSpeed;
            transform.Translate(move);
        }
    }
}
