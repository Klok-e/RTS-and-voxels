using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Scripts.Help
{
    public class MassJobThing
    {
        public List<JobHandle> _handles { get; }

        public MassJobThing(int size)
        {
            _handles = new List<JobHandle>(size);
        }

        public void AddHandle(JobHandle handle)
        {
            _handles.Add(handle);
        }

        public JobHandle CombineAll()
        {
            var arr = new NativeArray<JobHandle>(_handles.ToArray(), Allocator.TempJob);
            var handle = JobHandle.CombineDependencies(arr);
            _handles.Clear();
            arr.Dispose();
            return handle;
        }

        public void CompleteAll()
        {
            var handle = CombineAll();
            handle.Complete();
        }
    }
}
