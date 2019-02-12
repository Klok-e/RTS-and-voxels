using Scripts.World.Components;
using Unity.Entities;

namespace Scripts.World.Systems
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private ComponentGroup _needApply;

        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            _needApply = EntityManager.CreateComponentGroup(typeof(ChunkNeedMeshApply), typeof(RegularChunk));
            RequireForUpdate(_needApply);
        }

        protected override void OnUpdate()
        {
            var ents = _needApply.GetEntityArray();
            var chunks = _needApply.GetComponentArray<RegularChunk>();
            for(int i = 0; i < ents.Length; i++)
            {
                chunks[i].ApplyMeshData();
                PostUpdateCommands.RemoveComponent<ChunkNeedMeshApply>(ents[i]);
            }
        }
    }
}
