using System.Numerics;
using Newtonsoft.Json.Linq;

namespace OneInch.Net
{
    public class SwapInfo
    {
        public TokenInfo fromToken;
        public TokenInfo toToken;
        public BigInteger fromTokenAmount;
        public BigInteger toTokenAmount;
        public JArray protocols;
        public TxInfo tx;
    }
}