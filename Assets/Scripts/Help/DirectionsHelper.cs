using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Help
{
    public static class DirectionsHelper
    {
        /// <summary>
        /// x=-1 - left;
        /// y=-1 - down;
        /// z=-1 - back
        /// </summary>
        private static readonly Vector3Int[] DirectionsVec =
        {/*
            new Vector3Int(-1,-1,-1),
            new Vector3Int(-1,-1,0),
            new Vector3Int(-1,-1,1),
            new Vector3Int(-1,0,-1),
            new Vector3Int(-1,0,0),

            new Vector3Int(-1,0,1),
            new Vector3Int(-1,1,-1),
            new Vector3Int(-1,1,0),
            new Vector3Int(-1,1,1),
            new Vector3Int(0,-1,-1),

            new Vector3Int(0,-1,0),
            new Vector3Int(0,-1,1),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1),
            new Vector3Int(0,1,-1),

            new Vector3Int(0,1,0),
            new Vector3Int(0,1,1),
            new Vector3Int(1,-1,-1),
            new Vector3Int(1,-1,0),
            new Vector3Int(1,-1,1),

            new Vector3Int(1,0,-1),
            new Vector3Int(1,0,0),
            new Vector3Int(1,0,1),
            new Vector3Int(1,1,-1),
            new Vector3Int(1,1,0),

            new Vector3Int(1,1,1),
            */
            new Vector3Int(0,1,0),//up
            new Vector3Int(0,-1,0),//down
            new Vector3Int(-1,0,0),//left
            new Vector3Int(1,0,0),//right
            new Vector3Int(0,0,-1),//back
            new Vector3Int(0,0,1),//front
        };

        public static BlockDirectionFlag VecToDirection(this Vector3Int vec)
        {
            for (int i = 0; i < DirectionsVec.Length; i++)
            {
                if (vec == DirectionsVec[i])
                {
                    return (BlockDirectionFlag)(1 << i);
                }
            }
            throw new Exception();
        }

        public static Vector3Int ToVec(this BlockDirectionFlag en)
        {
            var vec = new Vector3Int();
            if ((en & BlockDirectionFlag.Up) != 0) vec += new Vector3Int(0, 1, 0);
            if ((en & BlockDirectionFlag.Down) != 0) vec += new Vector3Int(0, -1, 0);
            if ((en & BlockDirectionFlag.Left) != 0) vec += new Vector3Int(-1, 0, 0);
            if ((en & BlockDirectionFlag.Right) != 0) vec += new Vector3Int(1, 0, 0);
            if ((en & BlockDirectionFlag.Back) != 0) vec += new Vector3Int(0, 0, -1);
            if ((en & BlockDirectionFlag.Front) != 0) vec += new Vector3Int(0, 0, 1);
            return vec;
        }

        public static BlockDirectionFlag Opposite(this BlockDirectionFlag dir)
        {
            BlockDirectionFlag oppositeDir;
            if (dir == BlockDirectionFlag.Up || dir == BlockDirectionFlag.Left || dir == BlockDirectionFlag.Back)
                oppositeDir = (BlockDirectionFlag)(((byte)dir) << 1);
            else
                oppositeDir = (BlockDirectionFlag)(((byte)dir) >> 1);
            return oppositeDir;
        }

        public static class VectorDirections
        {
            public static readonly Vector3Int Up = new Vector3Int(0, 1, 0);
            public static readonly Vector3Int Down = new Vector3Int(0, -1, 0);
            public static readonly Vector3Int Left = new Vector3Int(-1, 0, 0);
            public static readonly Vector3Int Right = new Vector3Int(1, 0, 0);
            public static readonly Vector3Int Back = new Vector3Int(0, 0, -1);
            public static readonly Vector3Int Front = new Vector3Int(0, 0, 1);
        }

        public static Vector3Int ToInt(this Vector3 vec)
        {
            return new Vector3Int(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y), Mathf.RoundToInt(vec.z));
        }

        [Flags]
        public enum BlockDirectionFlag : byte
        {
            None = 0,
            Up = 1 << 0,
            Down = 1 << 1,
            Left = 1 << 2,
            Right = 1 << 3,
            Back = 1 << 4,
            Front = 1 << 5,
        }

        /*
        public enum DirectionsEnum : byte
        {
            LeftDownBack = 0,
            LeftDown = 1,
            LeftDownFront = 2,
            LeftBack = 3,
            Left = 4,

            LeftFront = 5,
            LeftUpBack = 6,
            LeftUp = 7,
            LeftUpFront = 8,
            DownBack = 9,

            Down = 10,
            DownFront = 11,
            Back = 12,
            Front = 13,
            UpBack = 14,

            Up = 15,
            UpFront = 16,
            RightDownBack = 17,
            RightDown = 18,
            RightDownFront = 19,

            RightBack = 20,
            Right = 21,
            RightFront = 22,
            RightUpBack = 23,
            RightUp = 24,

            RightUpFront = 25
         }

        public enum BlockDirections : byte
        {
            Up = 15,
            Down = 10,
            Left = 4,
            Right = 21,
            Back = 12,
            Front = 13,
        }*/
    }
}
