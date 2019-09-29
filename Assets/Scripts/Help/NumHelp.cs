using UnityEngine;

namespace Help
{
    public static class NumHelp
    {
        #region Normalize overloads

        public enum NormalizeRange
        {
            zero_one,
            minusOne_one
        }

        /// <param name="x">to normalize</param>
        /// <param name="minX">min value of x</param>
        /// <param name="maxX">max value of x</param>
        /// <param name="range">if true then normalizes to 0..1, else - -1..1</param>
        /// <returns></returns>
        public static double NormalizeNumber(double x, double minX, double maxX, NormalizeRange range)
        {
#if UNITY_EDITOR
            Debug.Assert(minX <= x && x <= maxX);
#endif

            double ans = (x - minX) / (maxX - minX);
            if (range == NormalizeRange.minusOne_one)
            {
                ans = -1 + 2 * ans;
#if UNITY_EDITOR
                Debug.Assert(-1 <= ans && ans <= 1);
#endif
            }
#if UNITY_EDITOR
            else if (range == NormalizeRange.zero_one)
            {
                Debug.Assert(0 <= ans && ans <= 1);
            }
#endif
            return ans;
        }

        /// <param name="x">to normalize</param>
        /// <param name="minX">min value of x</param>
        /// <param name="maxX">max value of x</param>
        /// <param name="range">if true then normalizes to 0..1, else - -1..1</param>
        /// <returns></returns>
        public static float NormalizeNumber(float x, float minX, float maxX, NormalizeRange range)
        {
#if UNITY_EDITOR
            Debug.Assert(minX <= x && x <= maxX);
#endif

            float ans = (x - minX) / (maxX - minX);
            if (range == NormalizeRange.minusOne_one)
            {
                ans = -1 + 2 * ans;
#if UNITY_EDITOR
                Debug.Assert(-1 <= ans && ans <= 1);
#endif
            }
#if UNITY_EDITOR
            else if (range == NormalizeRange.zero_one)
            {
                Debug.Assert(0 <= ans && ans <= 1);
            }
#endif
            return ans;
        }

        #endregion Normalize overloads
    }
}