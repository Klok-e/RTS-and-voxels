using Scripts.World.Components;
using Unity.Entities;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private ComponentGroup _needApply;

        [Inject]
        private EndFrameBarrier _barrier;

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _needApply = GetComponentGroup(typeof(ChunkNeedMeshApply), typeof(RegularChunk));
        }

        protected override void OnUpdate()
        {
            var commands = _barrier.CreateCommandBuffer();

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
