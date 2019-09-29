using System;
using Unity.Entities;
using Unity.Jobs;
using World.Components;

namespace World.Systems.ChunkHandling
{
    /// <summary>
    ///     Decides whether to load this chunk
    /// </summary>
    public class ChunkLoadManagerSystem : JobComponentSystem
    {
        private ChunkCreationSystem _chunkCreationSystem;

        protected override void OnCreate()
        {
            _chunkCreationSystem = World.GetOrCreateSystem<ChunkCreationSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
        }

        private struct DetermineWhetherToLoadFromDiscOrGenerateNewJob : IJobForEachWithEntity_EC<ChunkUnloaded>
        {
            public void Execute(Entity entity, int index, ref ChunkUnloaded c0)
            {
                throw new NotImplementedException();
            }
        }
    }
}