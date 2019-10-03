using System.Diagnostics;
using Unity.Entities;
using UnityEngine;
using World.Components;
using World.Systems.Regions;

namespace World.Systems.ChunkHandling
{
    public class ChunkMeshApplySystem : ComponentSystem
    {
        private RegionLoadUnloadSystem _chunkCreationSystem;
        private Stopwatch              _watch;

        protected override void OnCreate()
        {
            _chunkCreationSystem = World.GetOrCreateSystem<RegionLoadUnloadSystem>();
            _watch               = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            long milisBudget = (long) (Time.fixedDeltaTime * 1000f);
            _watch.Restart();
            var dict = _chunkCreationSystem.PosToChunk;
            Entities.WithAll<ChunkNeedMeshApply>().ForEach((Entity ent, ref ChunkPosComponent pos) =>
            {
                if (_watch.ElapsedMilliseconds >= milisBudget) return;
                dict[pos.Pos].ApplyMeshData();
                PostUpdateCommands.RemoveComponent<ChunkNeedMeshApply>(ent);
            });
            _watch.Stop();
        }
    }
}