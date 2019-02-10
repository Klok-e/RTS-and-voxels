using Unity.Entities;

namespace Assets.Scripts.PrototypingScripts
{
    public struct NeighboursComponent : IComponentData
    {
        public Entity left;
        public Entity right;
    }

    public struct SomeData : IComponentData
    {
        public int num;
    }
}
