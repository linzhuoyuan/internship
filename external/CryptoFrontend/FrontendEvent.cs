using System.Collections.Generic;

namespace MomCrypto.Frontend
{
    public class FrontendEvent
    {
        public const int MaxMsgSize = 1024 * 16;

        public byte[] Identity;
        public readonly List<byte[]> MsgData;
        public readonly List<int> MsgSize;

        public FrontendEvent(byte[] identity = null)
        {
            Identity = identity;
            MsgData = new List<byte[]>();
            MsgSize = new List<int>();
        }

        public FrontendEvent(byte[] identity, FrontendEvent e)
        {
            Identity = identity;
            MsgData = e.MsgData;
            MsgSize = e.MsgSize;
        }
    }
}
