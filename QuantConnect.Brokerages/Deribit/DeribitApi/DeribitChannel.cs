using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheOne.Deribit
{
    public enum DeribitChannelStatus
    {
        /// <summary>
        /// Reconnect 重置
        /// </summary>
        Reset,

        /// <summary>
        /// 已经发送订阅
        /// </summary>
        Subscribing,

        /// <summary>
        /// 订阅成功
        /// </summary>
        Subscribed,
    }

    public class DeribitChannel
    {
        public string Symbol { get; set; }
        public DeribitChannelStatus Status { get; set; }

        public string Channel { get; set; }

        public DateTime LastSendTime { get; set; }
    }
}
