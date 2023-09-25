using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// 
    /// </summary>
    public class KeyPool
    {
        private static HashSet<string> _keyPool = new HashSet<string>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool Contains(string key)
        {
            if(_keyPool.Contains(key))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        public static void AddKey(string key)
        {
            _keyPool.Add(key);
        }
    }
}
