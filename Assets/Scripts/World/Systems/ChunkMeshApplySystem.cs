using Scripts.World.Components;
using Unity.Entities;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private EntityCommandBufferSystem _commandBufferSys;

        protected override void OnCreate()
        {
            _commandBufferSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var commands = _commandBufferSys.CreateCommandBuffer();
            Entities.WithAll<ChunkNeedMeshApply>().ForEach((Entity ent, RegularChunk chunk) =>
            {
                chunk.ApplyMeshData();
                commands.RemoveComponent<ChunkNeedMeshApply>(ent);
            });
        }
    }
}
