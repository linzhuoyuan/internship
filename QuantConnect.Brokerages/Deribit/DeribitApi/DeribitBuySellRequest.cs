using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheOne.Deribit
{
    public class DeribitBuySellRequest
    {
        public string instrument_name { get; set; }

        public decimal amount { get; set; }

        public string type { get; set; }

        public string label { get; set; }

        public decimal? price { get; set; }

        public string time_in_force { get; set; }

        public decimal? max_show { get; set; }

        public bool post_only { get; set; }
        public bool reject_post_only { get; set; }

        public bool reduce_only { get; set; }

        public decimal? stop_price { get; set; }

        public string trigger { get; set; }

        public string advanced { get; set; }

        public bool mmp { get; set; }
    }
}
