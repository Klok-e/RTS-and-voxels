using Scripts.World;
using Scripts.World.Components;
using Scripts.World.Systems;
using Unity.Entities;

namespace Assets.Scripts.World.Systems
{
    [UpdateBefore(typeof(ChunkSystem))]
    public class ApplyVoxelChangesSystem : ComponentSystem
    {
        private ComponentGroup _chunksNeedApplyVoxelChanges;
        protected override void OnCreateManager()
        {
            _chunksNeedApplyVoxelChanges = EntityManager.CreateComponentGroup(typeof(RegularChunk), typeof(ChunkNeedApplyVoxelChanges));
        }

        protected override void OnUpdate()
        {
            var needApply = _chunksNeedApplyVoxelChanges.GetComponentArray<RegularChunk>();
            var applEntitties = _chunksNeedApplyVoxelChanges.GetEntityArray();
            for(int i = 0; i < needApply.Length; i++)
            {
                ApplyChangesToChunk(needApply[i]);
                PostUpdateCommands.RemoveComponent<ChunkNeedApplyVoxelChanges>(applEntitties[i]);
                PostUpdateCommands.AddComponent(applEntitties[i], new ChunkDirtyComponent());
            }
        }

        public void ApplyChangesToChunk(RegularChunk chunk)
        {
            while(chunk.VoxelSetQuery.Count > 0)
            {
                var x = chunk.VoxelSetQuery.Dequeue();
                var v = chunk.Voxels;
                v[x.Pos.x, x.Pos.y, x.Pos.z] = new Voxel
                {
                    type = x.NewVoxelType,
                };
            }
        }
    }
}
