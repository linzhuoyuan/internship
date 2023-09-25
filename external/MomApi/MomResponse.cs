namespace Quantmom.Api
{
    public class MomResponse
    {
        public byte[]? Identity;
        public byte MsgId;
        public bool Last;
        public uint Index;
        public MomAny Data = MomAny.Empty;
        public MomRspInfo RspInfo = null!;
    }
}