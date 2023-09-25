using System.Collections.Generic;
using Skyline;

namespace Quantmom.Frontend
{
    public class IdentityEqualsComparer : IEqualityComparer<byte[]>
    {
        public static readonly IdentityEqualsComparer Instance = new();

        public bool Equals(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
                return false;

            var common = new CommonArray();
            common.ByteArray = x;
            var array1 = common.UInt64Array!;
            common.ByteArray = y;
            var array2 = common.UInt64Array!;

            return array1[0] == array2[0] && array1[1] == array2[1];
        }

        public int GetHashCode(byte[] buffer)
        {
            var result = (uint)37;
            CommonArray commonArray = default;
            commonArray.ByteArray = buffer;
            var array = commonArray.UInt32Array!;
            var len = buffer.Length;
            for (var i = 0; i < len >> 2; i++)
            {
                var value = array[i];
                value = (uint)((int)value * -862048943);
                value = ((value << 15) | (value >> 17));
                value *= 461845907;
                result ^= value;
                result = ((result << 13) | (result >> 19));
                result = (uint)((int)(result * 5) + -430675100);
            }
            result = (uint)((int)result ^ len);
            result ^= result >> 16;
            result = (uint)((int)result * -2048144789);
            result ^= result >> 13;
            result = (uint)((int)result * -1028477387);
            return (int)(result ^ (result >> 16));
        }
    }
}
