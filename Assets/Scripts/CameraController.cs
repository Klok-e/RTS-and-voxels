using Scripts;
using Scripts.Help;
using Scripts.Pathfinding;
using Scripts.World;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;

namespace Scripts
{
    public enum ObjectTypes : byte
    {
        Voxel = 1,
        Sphere = 2,
        Light = 3,
    }

    public enum InteractionTypes : byte
    {
        Place = 1,
        Remove = 2,
    }

    public class CameraController : MonoBehaviour
    {
        [SerializeField]
        private float rotationSpeed = 1f;

        [SerializeField]
        private float moveSpeed = 3.5f;

        [SerializeField]
        private Text objectTypeLabel;

        [SerializeField]
        private Text voxelCoordinatesLabel;

        [SerializeField]
        private Slider sphereSizeSlider;

        private bool isPaused = false;

        private int _sphereSize = 3;

        private float X;
        private float Y;

        private ObjectTypes objectType = ObjectTypes.Voxel;
        private InteractionTypes interctionType = InteractionTypes.Place;

        #region MonoBehaviour implementation

        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            if (!isPaused)
            {
                ChangeVoxelCoord();
                ChangeObjectType();
                ChangeSphereSize();

                var leftClick = Input.GetMouseButtonDown(0);
                var rightClick = Input.GetMouseButtonDown(1);

                if (leftClick || rightClick)
                {
                    if (leftClick)
                        interctionType = InteractionTypes.Remove;
                    if (rightClick)
                        interctionType = InteractionTypes.Place;

                    Ray ray = new Ray(transform.position, transform.forward);

                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        var chunk = hit.collider.GetComponent<RegularChunk>();
                        if (chunk)
                        {
                            var world = chunk.Creator;
                            switch (interctionType)
                            {
                                case InteractionTypes.Place:
                                    var pos = hit.point + (hit.normal * VoxelWorld._blockSize / 2);
                                    switch (objectType)
                                    {
                                        case ObjectTypes.Voxel:
                                            world.QuerySetVoxel(pos, VoxelType.Dirt);
                                            break;

                                        case ObjectTypes.Sphere:
                                            world.QueryInsertSphere(pos, _sphereSize, VoxelType.Dirt);
                                            break;

                                        case ObjectTypes.Light:
                                            world.QuerySetLight(pos, 15);
                                            break;
                                    }
                                    break;

                                case InteractionTypes.Remove:
                                    pos = hit.point - (hit.normal * VoxelWorld._blockSize / 2);
                                    switch (objectType)
                                    {
                                        case ObjectTypes.Voxel:
                                            world.QuerySetVoxel(pos, VoxelType.Air);
                                            break;

                                        case ObjectTypes.Sphere:
                                            world.QueryInsertSphere(pos, _sphereSize, VoxelType.Air);
                                            break;

                                        case ObjectTypes.Light:
                                            world.QuerySetLight(pos, 15);
                                            break;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (!isPaused)
            {
                transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * rotationSpeed, Input.GetAxis("Mouse X") * rotationSpeed, 0));
                X = transform.rotation.eulerAngles.x;
                Y = transform.rotation.eulerAngles.y;
                transform.rotation = Quaternion.Euler(X, Y, 0);

                var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * moveSpeed;
                transform.Translate(move);
            }
        }

        #endregion MonoBehaviour implementation

        private void ChangeObjectType()
        {
            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0)
            {
                byte next;
                if (wheel > 0)
                {
                    next = (byte)(((byte)objectType) + 1);
                    if (next > 3)
                        next = 1;
                }
                else
                {
                    next = (byte)(((byte)objectType) - 1);
                    if (next < 1)
                        next = 3;
                }
                objectType = (ObjectTypes)next;
                objectTypeLabel.text = objectType.ToString();
            }
        }

        private void ChangeSphereSize()
        {
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                _sphereSize += 1;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                _sphereSize -= 1;
            }

            if (_sphereSize <= sphereSizeSlider.maxValue && _sphereSize >= sphereSizeSlider.minValue)
            {
            }
            else if (_sphereSize > sphereSizeSlider.maxValue)
                _sphereSize = (int)sphereSizeSlider.maxValue;
            else if (_sphereSize < sphereSizeSlider.minValue)
                _sphereSize = (int)sphereSizeSlider.minValue;

            sphereSizeSlider.value = _sphereSize;
        }

        private void ChangeVoxelCoord()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var pos = hit.point - (hit.normal * VoxelWorld._blockSize / 2);

                VoxelWorld.ChunkVoxelCoordinates(pos, out var chunk, out var voxel);
                voxelCoordinatesLabel.text = voxel.ToString();
            }
        }

        public void WhenEscPressed()
        {
            isPaused = !isPaused;
        }
    }
}
