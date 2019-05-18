using Scripts.World.Components;
using Unity.Entities;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        protected override void OnCreate()
        {
        }

        protected override void OnUpdate()
        {
            Entities.WithAll<ChunkNeedMeshApply>().ForEach((Entity ent, RegularChunk chunk) =>
            {
                chunk.ApplyMeshData();
                PostUpdateCommands.RemoveComponent<ChunkNeedMeshApply>(ent);
            });
        }
    }
}
