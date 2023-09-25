using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Logging
{
    /// <summary>
    /// 
    /// </summary>
    public class LogUtil
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static String GetStackFrameLocationInfo()
        {
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);
            StackFrame sf = st.GetFrame(3);
            if (sf == null)
            {
                return "[unknownClass]";
            }
            string className = System.IO.Path.GetFileNameWithoutExtension(sf.GetFileName());
            return String.Format(@"[{0}.{1} {2}]",
                className,
                sf.GetMethod(),
                sf.GetFileLineNumber());
        }
    }
}
