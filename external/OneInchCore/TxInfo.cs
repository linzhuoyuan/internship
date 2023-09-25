using System.Numerics;

namespace OneInch.Net
{
    public class TxInfo
    {
        public string from;
        public string to;
        public string data;
        public BigInteger value;
        public int gas;
        public int gasPrice;
    }
}