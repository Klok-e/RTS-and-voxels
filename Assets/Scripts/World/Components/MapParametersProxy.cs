using System;
using Unity.Entities;
using UnityEngine;

namespace Scripts.World.Components
{
    [Serializable]
    public struct MapParameters : ISharedComponentData, IEquatable<MapParameters>
    {
        public Material _chunkMaterial;
        public Texture2D[] _textures;

        public bool Equals(MapParameters other)
        {
            return _textures == other._textures && _chunkMaterial == other._chunkMaterial;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [RequiresEntityConversion]
    public class MapParametersProxy : MonoBehaviour, IConvertGameObjectToEntity
    {
        [SerializeField]
        private Material _chunkMaterial;
        [SerializeField]
        private Texture2D[] _textures;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem converstionSystem)
        {
            var comp = new MapParameters { _chunkMaterial = _chunkMaterial, _textures = _textures };
            dstManager.AddSharedComponentData(entity, comp);
        }
    }
}
