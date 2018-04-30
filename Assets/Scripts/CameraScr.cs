using Scripts.Help;
using Scripts.World;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace MarchingCubesProject
{
    public class CameraScr : MonoBehaviour
    {
        public float rotationSpeed = 1f;
        public float moveSpeed = 3.5f;
        private float X;
        private float Y;

        [SerializeField]
        private bool _isBlockInsertion;

        private void Start()
        {
            RenderSettings.fog = false;
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
                        var pos = hit.point + (hit.normal * -VoxelWorld._blockSize / 2);
                        if (_isBlockInsertion)
                        {
                            //pos /= VoxelWorld._chunkSize;

                            pos -= (Vector3)chunk.Pos * VoxelWorld._chunkSize * VoxelWorld._blockSize;//relative to chunk
                            pos /= VoxelWorld._blockSize;//to block coords

                            var posInt = pos.ToInt();
                            VoxelWorld.Instance.SetVoxel(chunk.Pos, posInt, VoxelType.Air);
                        }
                        else
                        {
                            VoxelWorld.Instance.InsertSphere(pos, 3, VoxelType.Air);
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
