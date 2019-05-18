using Scripts.World.Components;
using Unity.Entities;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private EntityQuery _needApply;

        private EntityCommandBufferSystem _commandBufferSys;

        protected override void OnCreate()
        {
            _needApply = GetEntityQuery(typeof(ChunkNeedMeshApply), typeof(RegularChunk));
            _commandBufferSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var commands = _commandBufferSys.CreateCommandBuffer();

            var ents = _needApply.GetEntityArray();
            var chunks = _needApply.GetComponentArray<RegularChunk>();
            for(int i = 0; i < ents.Length; i++)
            {
                chunks[i].ApplyMeshData();
                commands.RemoveComponent<ChunkNeedMeshApply>(ents[i]);
            }
        }
    }
}
