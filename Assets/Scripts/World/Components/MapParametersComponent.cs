using System;
using Unity.Entities;
using UnityEngine;

namespace Scripts.World.Components
{
    [Serializable]
    public struct MapParameters : ISharedComponentData
    {
        public Vector2Int _size;
        public Material _chunkMaterial;
        public Texture2D[] _textures;
    }

    public class MapParametersComponent : SharedComponentDataWrapper<MapParameters>
    {
    }
}
