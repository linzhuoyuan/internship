using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Util
{
    public static class SupportOffset
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsSupportOffset(Symbol symbol)
        {
            //中国市场的期权和期货
            return symbol.IsChinaMarket();
        }

    }
}
