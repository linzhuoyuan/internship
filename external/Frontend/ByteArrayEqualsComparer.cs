﻿using System.Collections.Generic;
using Skyline;

namespace Quantmom.Frontend
{
    public class ByteArrayEqualsComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayEqualsComparer Instance = new ByteArrayEqualsComparer();

        public bool Equals(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
                return false;

            var common = new CommonArray();
            common.ByteArray = x;
            var array1 = common.UInt64Array!;
            common.ByteArray = y;
            var array2 = common.UInt64Array!;

            var length = x.Length;
            var len = length >> 3;
            var remainder = length & 7;

            var i = len;

            if (remainder > 0)
            {
                var shift = sizeof(ulong) - remainder;
                if ((array1[i] << shift) >> shift != (array2[i] << shift) >> shift)
                    return false;
            }

            i--;

            while (i >= 7)
            {
                if (array1[i] != array2[i] ||
                    array1[i - 1] != array2[i - 1] ||
                    array1[i - 2] != array2[i - 2] ||
                    array1[i - 3] != array2[i - 3] ||
                    array1[i - 4] != array2[i - 4] ||
                    array1[i - 5] != array2[i - 5] ||
                    array1[i - 6] != array2[i - 6] ||
                    array1[i - 7] != array2[i - 7])
                    return false;

                i -= 8;
            }

            if (i >= 3)
            {
                if (array1[i] != array2[i] ||
                    array1[i - 1] != array2[i - 1] ||
                    array1[i - 2] != array2[i - 2] ||
                    array1[i - 3] != array2[i - 3])
                    return false;

                i -= 4;
            }

            if (i >= 1)
            {
                if (array1[i] != array2[i] ||
                    array1[i - 1] != array2[i - 1])
                    return false;

                i -= 2;
            }

            if (i < 0)
            {
                return true;
            }

            return array1[i] == array2[i];

            //i -= 1;
        }

        public int GetHashCode(byte[] obj)
        {
            return obj.GetHashCodeEx();
        }
    }
}
