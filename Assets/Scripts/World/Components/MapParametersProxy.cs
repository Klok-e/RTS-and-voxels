using System;
using Unity.Entities;
using UnityEngine;

namespace Scripts.World.Components
{
    [Serializable]
    public struct MapParameters : ISharedComponentData
    {
        public Material _chunkMaterial;
        public Texture2D[] _textures;
    }

    // TODO: In new version it must work
    //[GameObjectToEntityConversion] //or ConvertToEntity
    //public class MapParametersProxy : MonoBehaviour, IConvertGameObjectToEntity
    //{
    //    [SerializeField]
    //    private Vector2Int _size;
    //    [SerializeField]
    //    private Material _chunkMaterial;
    //    [SerializeField]
    //    private Texture2D[] _textures;
    //
    //    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem converstionSystem)
    //    {
    //        var comp = new MapParameters { _size = _size, _chunkMaterial = _chunkMaterial, _textures = _textures };
    //        dstManager.AddSharedComponentData(entity, comp);
    //    }
    //}

    public class MapParametersProxy : SharedComponentDataWrapper<MapParameters>
    {
    }
}
