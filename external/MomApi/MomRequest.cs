using System;

namespace Quantmom.Api
{
    public class MomRequest
    {
        public byte[] Identity = Array.Empty<byte>();
        public byte MsgId;
        public MomAny Data = MomAny.Empty;
    }
}