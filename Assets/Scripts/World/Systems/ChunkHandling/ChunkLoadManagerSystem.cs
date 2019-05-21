using Scripts.World.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Jobs;

namespace Scripts.World.Systems
{
    /// <summary>
    /// Decides whether to load this chunk
    /// </summary>
    public class ChunkLoadManagerSystem : JobComponentSystem
    {
        private ChunkCreationSystem _chunkCreationSystem;

        private struct DetermineWhetherToLoadFromDiscOrGenerateNewJob : IJobForEachWithEntity_EC<ChunkUnloaded>
        {
            public void Execute(Entity entity, int index, ref ChunkUnloaded c0)
            {
                throw new NotImplementedException();
            }
        }

        protected override void OnCreate()
        {
            _chunkCreationSystem = World.GetOrCreateSystem<ChunkCreationSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
        }
    }
}
