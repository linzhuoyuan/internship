namespace MomCrypto.Api
{
    public class MomResponse
    {
        public byte[]? Identity;
        public byte MsgId;
        public bool Last;
        public uint Index;
        public MomAny Data;
        public MomRspInfo RspInfo;
    }
}