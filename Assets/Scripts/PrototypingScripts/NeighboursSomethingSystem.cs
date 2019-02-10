using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Assets.Scripts.PrototypingScripts
{
    public class NeighboursSomethingSystem : ComponentSystem
    {
        private Entity myEntity;

        private struct MyJob : IJob
        {
            public Entity toProcess;

            public void Execute()
            {



            }
        }
        protected override void OnCreateManager()
        {
            myEntity = EntityManager.CreateEntity(typeof(NeighboursComponent), typeof(SomeData));
            EntityManager.SetComponentData(myEntity, new SomeData() { num = 42, });
        }

        protected override void OnUpdate()
        {

            new MyJob()
            {
                toProcess = myEntity
            }.Schedule().Complete();
        }
    }

    internal struct Instance : IComponentData
    {
        public float f;
        // public DynamicBuffer <int> db_a ;
    }

    internal struct SomeBufferElement : IBufferElementData
    {
        public int i;
    }

    public class BufferWithJobSystem : JobComponentSystem
    {
        [BurstCompile]
        [RequireComponentTag(typeof(SomeBufferElement))]
        private struct Job : IJobProcessComponentDataWithEntity<Instance>
        {
            public float dt;

            // Allow buffer read write in parralel jobs
            // Ensure, no two jobs can write to same entity, at the same time.
            // !! "You are somehow completely certain that there is no race condition possible here, because you are absolutely certain that you will not be writing to the same Entity ID multiple times from your parallel for job. (If you do thats a race condition and you can easily crash unity, overwrite memory etc) If you are indeed certain and ready to take the risks.
            // https://forum.unity.com/threads/how-can-i-improve-or-jobify-this-system-building-a-list.547324/#post-3614833
            [NativeDisableParallelForRestriction]
            public BufferFromEntity<SomeBufferElement> someBufferElement;

            public void Execute(Entity entity, int index, ref Instance tester)
            {
                tester.f = 10 * dt;

                DynamicBuffer<SomeBufferElement> someDynamicBuffer = someBufferElement[entity];

                SomeBufferElement buffer = someDynamicBuffer[0];

                // Uncomment as needed
                // buffer.i = 99 ;

                // someDynamicBuffer [0] = buffer ;

                // Debug Will throw errors in Job system
                // Debug.Log ( "#" + index + "; " + someDynamicBuffer [0].i + "; " + someDynamicBuffer [1].i ) ;

            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new Job()
            {
                dt = Time.deltaTime,
                someBufferElement = GetBufferFromEntity<SomeBufferElement>(false)

            };
            return job.Schedule(this, inputDeps);
        }

        // protected override void OnCreateManager ( ) // for Entities 0.0.12 preview 20
        protected override void OnCreateManager()
        {
            base.OnCreateManager();

            Instance instance = new Instance();

            Entity entity = EntityManager.CreateEntity(typeof(Instance));

            EntityManager.SetComponentData(entity, instance);
            EntityManager.AddBuffer<SomeBufferElement>(entity);

            var bufferFromEntity = GetBufferFromEntity<SomeBufferElement>();
            var buffer = bufferFromEntity[entity];

            SomeBufferElement someBufferElement = new SomeBufferElement();
            someBufferElement.i = 6;
            buffer.Add(someBufferElement);
            someBufferElement.i = 7;
            buffer.Add(someBufferElement);
        }
    }

}
