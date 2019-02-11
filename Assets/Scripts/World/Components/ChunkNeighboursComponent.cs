using Scripts.Help;
using Unity.Entities;

namespace Scripts.World.Components
{
    public struct ChunkNeighboursComponent : IComponentData
    {
        public Entity Left { get; set; }
        public Entity Right { get; set; }
        public Entity Up { get; set; }
        public Entity Down { get; set; }
        public Entity Forward { get; set; }
        public Entity Backward { get; set; }

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
