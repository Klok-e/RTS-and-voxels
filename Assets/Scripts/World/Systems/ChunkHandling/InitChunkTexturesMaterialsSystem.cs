using System;
using JetBrains.Annotations;
using Unity.Entities;
using UnityEngine;
using World.Components;

namespace World.Systems.ChunkHandling
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class InitChunkTexturesMaterialsSystem : ComponentSystem
    {
        private static readonly int VoxelTextureArray = Shader.PropertyToID("_VoxelTextureArray");

        public Material   ChunkMaterial { get; private set; }
        public Vector2Int MapSize       { get; private set; }

        protected override void OnUpdate()
        {
            bool once = false;
            Entities.ForEach((Entity ent, MapParameters parameters) =>
            {
                if (once)
                    throw new Exception("Only one MapParameters instance allowed at a time.");
                once = true;

                PostUpdateCommands.DestroyEntity(ent);

                ChunkMaterial = parameters._chunkMaterial;

                SetTextureArray(parameters._textures);
            });
        }

        private void SetTextureArray(Texture2D[] textures)
        {
            var textureArray = new Texture2DArray(16, 16, textures.Length, TextureFormat.RGBA32, true);
            for (int i = 0; i < textures.Length; i++)
            {
                var pix = textures[i].GetPixels();
                textureArray.SetPixels(pix, i);
            }

            textureArray.Apply();

            textureArray.filterMode = FilterMode.Point;

            ChunkMaterial.SetTexture(VoxelTextureArray, textureArray);
        }
    }
}