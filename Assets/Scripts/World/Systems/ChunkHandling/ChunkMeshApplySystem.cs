using Unity.Entities;
using World.Components;
using World.Systems.Regions;

namespace World.Systems.ChunkHandling
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