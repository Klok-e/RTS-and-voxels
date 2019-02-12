using Scripts.Help;
using Unity.Entities;

namespace Scripts.World.Components
{
    public struct ChunkNeighboursComponent : IComponentData
    {
        public Entity Left;
        public Entity Right;
        public Entity Up;
        public Entity Down;
        public Entity Forward;
        public Entity Backward;

        public Entity this[DirectionsHelper.BlockDirectionFlag dir]
        {
            get
            {
                switch(dir)
                {
                    case DirectionsHelper.BlockDirectionFlag.Up:
                        return Up;
                    case DirectionsHelper.BlockDirectionFlag.Down:
                        return Down;
                    case DirectionsHelper.BlockDirectionFlag.Left:
                        return Left;
                    case DirectionsHelper.BlockDirectionFlag.Right:
                        return Right;
                    case DirectionsHelper.BlockDirectionFlag.Backward:
                        return Backward;
                    case DirectionsHelper.BlockDirectionFlag.Forward:
                        return Forward;
                    case DirectionsHelper.BlockDirectionFlag.None:
                        return Entity.Null;
                    default:
                        return Entity.Null;
                }
            }
            set
            {
                switch(dir)
                {
                    case DirectionsHelper.BlockDirectionFlag.Up:
                        Up = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Down:
                        Down = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Left:
                        Left = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Right:
                        Right = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Backward:
                        Backward = value;
                        break;
                    case DirectionsHelper.BlockDirectionFlag.Forward:
                        Forward = value;
                        break;
                }
            }
        }
    }
}
