using Scripts.World.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEngine;

namespace Scripts.World.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class InitChunkTexturesMaterialsSystem : ComponentSystem
    {
        public Material _chunkMaterial { get; private set; }
        public Vector2Int _mapSize { get; private set; }

        protected override void OnUpdate()
        {
            bool once = false;
            Entities.ForEach((Entity ent, MapParameters parameters) =>
            {
                if(once)
                    throw new Exception("Only one MapParameters instance allowed at a time.");
                once = true;

                PostUpdateCommands.DestroyEntity(ent);

                _chunkMaterial = parameters._chunkMaterial;

                SetTextureArray(parameters._textures);
            });
        }

        private void SetTextureArray(Texture2D[] textures)
        {
            var textureArray = new Texture2DArray(16, 16, textures.Length, TextureFormat.RGBA32, true);
            for(int i = 0; i < textures.Length; i++)
            {
                var pix = textures[i].GetPixels();
                textureArray.SetPixels(pix, i);
            }
            textureArray.Apply();

            textureArray.filterMode = FilterMode.Point;

            _chunkMaterial.SetTexture("_VoxelTextureArray", textureArray);
        }
    }
}
