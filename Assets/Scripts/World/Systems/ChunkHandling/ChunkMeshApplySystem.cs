using Scripts.World.Components;
using Scripts.World.Systems.Regions;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private RegionLoadUnloadSystem _chunkCreationSystem;

        protected override void OnCreate()
        {
            _chunkCreationSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
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
