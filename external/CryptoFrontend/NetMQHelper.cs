using System;
using System.Reflection;
using AsyncIO;

namespace MomCrypto.Frontend
{
    public static class NetMQHelper
    {
        private static readonly PropertyInfo SocketHandle;
        private static readonly Type WinSocketType;

        static NetMQHelper()
        {
            WinSocketType = typeof(AsyncSocket).Assembly.GetType("AsyncIO.Windows.Socket");
            if (WinSocketType != null) {
                SocketHandle = WinSocketType.GetProperty("Handle");
            }
        }

        public static int GetSocketHandle(this AsyncSocket socket)
        {
            if (socket.GetType() == WinSocketType && SocketHandle != null) {
                return ((IntPtr)SocketHandle.GetValue(socket)).ToInt32();
            }
            return -1;
        }
    }
}
