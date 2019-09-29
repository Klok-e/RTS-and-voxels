using System;
using Unity.Mathematics;
using UnityEngine;
using World;

namespace Help
{
    public static class DirectionsHelper
    {
        [Flags]
        public enum BlockDirectionFlag : byte
        {
            None     = 0,
            Up       = 1 << 0,
            Down     = 1 << 1,
            Left     = 1 << 2,
            Right    = 1 << 3,
            Backward = 1 << 4,
            Forward  = 1 << 5
        }

        /// <summary>
        ///     x=-1 - left;
        ///     y=-1 - down;
        ///     z=-1 - back
        /// </summary>
        private static readonly Vector3Int[] DirectionsVec =
        {
            /*
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
            new Vector3Int(0,  1,  0),  //up
            new Vector3Int(0,  -1, 0),  //down
            new Vector3Int(-1, 0,  0),  //left
            new Vector3Int(1,  0,  0),  //right
            new Vector3Int(0,  0,  -1), //backward
            new Vector3Int(0,  0,  1)   //forward
        };

        public static BlockDirectionFlag VecToDirection(this Vector3Int vec)
        {
            for (int i = 0; i < DirectionsVec.Length; i++)
                if (vec == DirectionsVec[i])
                    return (BlockDirectionFlag) (1 << i);
            throw new Exception();
        }

        //[Obsolete("Use ToInt3")]
        public static Vector3Int ToVecInt(this BlockDirectionFlag en)
        {
            var vec = new Vector3Int();
            if ((en & BlockDirectionFlag.Up) != 0)
                vec += new Vector3Int(0, 1, 0);
            if ((en & BlockDirectionFlag.Down) != 0)
                vec += new Vector3Int(0, -1, 0);
            if ((en & BlockDirectionFlag.Left) != 0)
                vec += new Vector3Int(-1, 0, 0);
            if ((en & BlockDirectionFlag.Right) != 0)
                vec += new Vector3Int(1, 0, 0);
            if ((en & BlockDirectionFlag.Backward) != 0)
                vec += new Vector3Int(0, 0, -1);
            if ((en & BlockDirectionFlag.Forward) != 0)
                vec += new Vector3Int(0, 0, 1);
            return vec;
        }

        public static int3 ToInt3(this BlockDirectionFlag en)
        {
            var vec = new int3();
            if ((en & BlockDirectionFlag.Up) != 0)
                vec += new int3(0, 1, 0);
            if ((en & BlockDirectionFlag.Down) != 0)
                vec += new int3(0, -1, 0);
            if ((en & BlockDirectionFlag.Left) != 0)
                vec += new int3(-1, 0, 0);
            if ((en & BlockDirectionFlag.Right) != 0)
                vec += new int3(1, 0, 0);
            if ((en & BlockDirectionFlag.Backward) != 0)
                vec += new int3(0, 0, -1);
            if ((en & BlockDirectionFlag.Forward) != 0)
                vec += new int3(0, 0, 1);
            return vec;
        }

        public static Vector3 ToVecFloat(this BlockDirectionFlag en)
        {
            var vec = new Vector3();
            if ((en & BlockDirectionFlag.Up) != 0)
                vec += new Vector3(0, 1, 0);
            if ((en & BlockDirectionFlag.Down) != 0)
                vec += new Vector3(0, -1, 0);
            if ((en & BlockDirectionFlag.Left) != 0)
                vec += new Vector3(-1, 0, 0);
            if ((en & BlockDirectionFlag.Right) != 0)
                vec += new Vector3(1, 0, 0);
            if ((en & BlockDirectionFlag.Backward) != 0)
                vec += new Vector3(0, 0, -1);
            if ((en & BlockDirectionFlag.Forward) != 0)
                vec += new Vector3(0, 0, 1);
            return vec;
        }

        public static BlockDirectionFlag Opposite(this BlockDirectionFlag dir)
        {
            BlockDirectionFlag oppositeDir;
            if (dir == BlockDirectionFlag.Up || dir == BlockDirectionFlag.Left || dir == BlockDirectionFlag.Backward)
                oppositeDir = (BlockDirectionFlag) ((byte) dir << 1);
            else
                oppositeDir = (BlockDirectionFlag) ((byte) dir >> 1);
            return oppositeDir;
        }

        public static Vector3Int ToVecInt(this Vector3 vec)
        {
            return new Vector3Int(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y), Mathf.RoundToInt(vec.z));
        }

        public static BlockDirectionFlag WrapCoordsInChunk(ref int x, ref int y, ref int z)
        {
            var dirWrapped = BlockDirectionFlag.None;
            if (x >= VoxConsts._chunkSize)
            {
                x          =  0;
                dirWrapped |= BlockDirectionFlag.Right;
            }

            if (y >= VoxConsts._chunkSize)
            {
                y          =  0;
                dirWrapped |= BlockDirectionFlag.Up;
            }

            if (z >= VoxConsts._chunkSize)
            {
                z          =  0;
                dirWrapped |= BlockDirectionFlag.Forward;
            }

            if (x < 0)
            {
                x          =  VoxConsts._chunkSize - 1;
                dirWrapped |= BlockDirectionFlag.Left;
            }

            if (y < 0)
            {
                y          =  VoxConsts._chunkSize - 1;
                dirWrapped |= BlockDirectionFlag.Down;
            }

            if (z < 0)
            {
                z          =  VoxConsts._chunkSize - 1;
                dirWrapped |= BlockDirectionFlag.Backward;
            }

            return dirWrapped;
        }

        public static bool AreCoordsOutOfBordersOfChunk(int x, int y, int z)
        {
            return x >= VoxConsts._chunkSize || x < 0
                                             ||
                                             y >= VoxConsts._chunkSize || y < 0
                                             ||
                                             z >= VoxConsts._chunkSize || z < 0;
        }

        public static BlockDirectionFlag AreCoordsAtBordersOfChunk(int3 coords)
        {
            var res = BlockDirectionFlag.None;
            if (coords.x == VoxConsts._chunkSize - 1)
                res |= BlockDirectionFlag.Right;
            if (coords.x == 0)
                res |= BlockDirectionFlag.Left;
            if (coords.y == VoxConsts._chunkSize - 1)
                res |= BlockDirectionFlag.Up;
            if (coords.y == 0)
                res |= BlockDirectionFlag.Down;
            if (coords.z == VoxConsts._chunkSize - 1)
                res |= BlockDirectionFlag.Forward;
            if (coords.z == 0)
                res |= BlockDirectionFlag.Backward;
            return res;
        }

        public static class VectorDirections
        {
            public static readonly Vector3Int Up       = new Vector3Int(0,  1,  0);
            public static readonly Vector3Int Down     = new Vector3Int(0,  -1, 0);
            public static readonly Vector3Int Left     = new Vector3Int(-1, 0,  0);
            public static readonly Vector3Int Right    = new Vector3Int(1,  0,  0);
            public static readonly Vector3Int Backward = new Vector3Int(0,  0,  -1);
            public static readonly Vector3Int Forward  = new Vector3Int(0,  0,  1);
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