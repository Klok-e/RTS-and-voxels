using Scripts.Help;
using Scripts.World;
using Scripts.World.Components;
using Scripts.World.DynamicBuffers;
using Scripts.World.Systems.Interaction;
using Scripts.World.Utils;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Scripts
{
    public enum ObjectTypes : byte
    {
        Voxel = 1,
        Sphere = 2,
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
        private Text voxelTypeLabel;

        [SerializeField]
        private Text voxelCoordinatesLabel;

        [SerializeField]
        private Slider sphereSizeSlider;

        private bool isPaused = false;

        private uint _sphereSize = 3;

        private float _x;
        private float _y;

        private ObjectTypes objectType = ObjectTypes.Voxel;
        private VoxelType voxType = VoxelType.Dirt;

        private ChangeTerrainVoxelSystem _changeTerrainVoxelSystem;

        #region MonoBehaviour implementation

        private void Start()
        {
            //Cursor.visible = false;
            //Cursor.lockState = CursorLockMode.Locked;
            _changeTerrainVoxelSystem = Unity.Entities.World.Active.GetOrCreateSystem<ChangeTerrainVoxelSystem>();
        }

        private void Update()
        {
            if(!isPaused)
            {
                ChangeVoxelCoord();
                //ChangeObjectType();
                //ChangeSphereSize();
                ChangeVoxelType();

                var removeBlock = Input.GetMouseButtonDown(0);
                var placeBlock = Input.GetMouseButtonDown(1);

                // if one of them but not both
                if(removeBlock ^ placeBlock)
                {
                    var ray = new Ray(transform.position, transform.forward);
                    if(Physics.Raycast(ray, out var hit, VoxConsts._blockSize * 10))
                    {
                        var pos = math.float3(0);
                        var voxelType = VoxelType.Dirt;
                        if(removeBlock)
                        {
                            pos = hit.point - (VoxConsts._blockSize * hit.normal / 2f);
                            voxelType = VoxelType.Empty;
                        }
                        else if(placeBlock)
                        {
                            pos = hit.point + (VoxConsts._blockSize * hit.normal / 2f);
                            voxelType = voxType;
                        }

                        switch(objectType)
                        {
                            case ObjectTypes.Voxel:
                                var chunk = VoxConsts.ChunkIn(pos);
                                var ind = VoxConsts.VoxIndexInChunk(pos, chunk);

                                //Debug.Log($"Ch: {chunk.ToString()}, Ind: {ind.ToString()}");

                                _changeTerrainVoxelSystem.ToSetPos.Enqueue(new ChangeTerrainVoxelSystem.SetPos
                                {
                                    Chunk = chunk,
                                    Coord = ind,
                                    VoxelType = voxelType
                                });
                                break;

                            case ObjectTypes.Sphere:
                                //VoxelInteractionUtils.SetQuerySphere(gameEntity.Entity, gameEntity.EntityManager, index, _sphereSize, VoxelType.Empty);
                                Debug.LogError("Must be unreachable");
                                break;
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if(!isPaused)
            {
                transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * rotationSpeed, Input.GetAxis("Mouse X") * rotationSpeed, 0));
                _x = transform.rotation.eulerAngles.x;
                _y = transform.rotation.eulerAngles.y;
                transform.rotation = Quaternion.Euler(_x, _y, 0);

                var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * moveSpeed;
                transform.Translate(move);
            }
        }

        #endregion MonoBehaviour implementation

        [Obsolete]
        private void ChangeObjectType()
        {
            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if(Mathf.Abs(wheel) > 0)
            {
                byte next;
                if(wheel > 0)
                {
                    next = (byte)(((byte)objectType) + 1);
                    if(next > 2)
                        next = 1;
                }
                else
                {
                    next = (byte)(((byte)objectType) - 1);
                    if(next < 1)
                        next = 2;
                }
                objectType = (ObjectTypes)next;
                objectTypeLabel.text = objectType.ToString();
            }
        }

        private void ChangeVoxelType()
        {
            if(Input.GetKey(KeyCode.Alpha1))
            {
                voxType = VoxelType.Dirt;
            }
            else if(Input.GetKey(KeyCode.Alpha2))
            {
                voxType = VoxelType.Lamp;
            }
            voxelTypeLabel.text = voxType.ToString();
        }

        [Obsolete]
        private void ChangeSphereSize()
        {
            if(Input.GetKeyDown(KeyCode.PageUp))
            {
                _sphereSize += 1;
            }
            if(Input.GetKeyDown(KeyCode.PageDown))
            {
                _sphereSize -= 1;
            }

            if(_sphereSize <= sphereSizeSlider.maxValue && _sphereSize >= sphereSizeSlider.minValue)
            {
            }
            else if(_sphereSize > sphereSizeSlider.maxValue)
                _sphereSize = (uint)sphereSizeSlider.maxValue;
            else if(_sphereSize < sphereSizeSlider.minValue)
                _sphereSize = (uint)sphereSizeSlider.minValue;

            sphereSizeSlider.value = _sphereSize;
        }

        private void ChangeVoxelCoord()
        {
            var ray = new Ray(transform.position, transform.forward);
            if(Physics.Raycast(ray, out var hit))
            {
                var point = hit.point - (hit.normal / 2f);
                var chunk = VoxConsts.ChunkIn(point);
                var voxCoord = VoxConsts.VoxIndexInChunk(point, chunk);
                voxelCoordinatesLabel.text = $"Voxel: {voxCoord.ToString()}\nChunk: {chunk.ToString()}";
            }
        }

        public void WhenEscPressed()
        {
            isPaused = !isPaused;
        }
    }
}
