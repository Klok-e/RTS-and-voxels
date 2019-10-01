using Unity.Entities;
using Unity.Mathematics;

namespace World.DynamicBuffers
{
    public enum SetLightType : byte
    {
        Sunlight     = 0,
        RegularLight = 1
    }

    public enum PropagationType : byte
    {
        Propagate   = 2,
        Depropagate = 1,
        Regular     = 0
    }

    [InternalBufferCapacity(0)]
    public struct LightSetQueryData : IBufferElementData
    {
        public int3            pos;
        public byte            newLight;
        public SetLightType    lightType;
        public PropagationType propagation;
    }
}