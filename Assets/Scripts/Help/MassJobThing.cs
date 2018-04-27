using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public void CompleteAll()
        {
            foreach (var item in _handles)
            {
                item.Complete();
            }
            _handles.Clear();
        }
    }
}
