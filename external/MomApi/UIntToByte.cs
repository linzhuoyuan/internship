using System.Runtime.InteropServices;

namespace Quantmom.Api
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntToByte
    {
        [FieldOffset(0)]
        public uint IntVal;

        [FieldOffset(0)]
        public byte b0;
        [FieldOffset(1)]
        public byte b1;
        [FieldOffset(2)]
        public byte b2;
        [FieldOffset(3)]
        public byte b3;
    }
}