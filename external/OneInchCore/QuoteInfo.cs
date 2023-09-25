using System.Numerics;
using Newtonsoft.Json.Linq;

namespace OneInch.Net
{
    public class QuoteInfo
    {
        public TokenInfo fromToken;
        public TokenInfo toToken;
        public BigInteger fromTokenAmount;
        public BigInteger toTokenAmount;
        public JArray protocols;
        public int estimatedGas;
    }
}