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

        public static Vector3Int DirectionToVec(this BlockDirectionFlag en)
        {
            //return DirectionsVec[Mathf.RoundToInt((Mathf.Log10((byte)en) / Mathf.Log10(2)))];
            if (en == BlockDirectionFlag.Up) return new Vector3Int(0, 1, 0);
            else if (en == BlockDirectionFlag.Down) return new Vector3Int(0, -1, 0);
            else if (en == BlockDirectionFlag.Left) return new Vector3Int(-1, 0, 0);
            else if (en == BlockDirectionFlag.Right) return new Vector3Int(1, 0, 0);
            else if (en == BlockDirectionFlag.Back) return new Vector3Int(0, 0, -1);
            else if (en == BlockDirectionFlag.Front) return new Vector3Int(0, 0, 1);
            else return new Vector3Int(0, 0, 0);
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
