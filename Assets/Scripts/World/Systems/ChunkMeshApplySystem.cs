using Scripts.World.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private ChunkCreationSystem _chunkCreationSystem;

        protected override void OnCreate()
        {
            _chunkCreationSystem = World.GetOrCreateSystem<ChunkCreationSystem>();
        }

        protected override void OnUpdate()
        {
            var dict = _chunkCreationSystem.PosToChunk;
            Entities.WithAll<ChunkNeedMeshApply>().ForEach((Entity ent, ref ChunkPosComponent pos) =>
            {
                dict[pos.Pos].ApplyMeshData();
                PostUpdateCommands.RemoveComponent<ChunkNeedMeshApply>(ent);
            });
        }
    }
}
