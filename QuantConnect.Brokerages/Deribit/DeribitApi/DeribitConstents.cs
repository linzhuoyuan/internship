using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheOne.Deribit
{
    static class DeribitConstents
    {
        public const int SUBSCRIBE_ID = 1;
        public const int UNSUBSCRIBE_ID = 2;
        public const int AUTHENTICATE_ID = 3;
        public const int REFRESH_TOKEN_ID = 4;
        public const int SET_HEART_BEAT_ID = 5;
        public const int TEST_ID = 6;

        public const int HEARTBEAT_SPAN = 10;
        public const int ACTIVE_TEST = 2;
        public const int RefreshAuthTokenLoopPeriodMins = 5;
    }
}
