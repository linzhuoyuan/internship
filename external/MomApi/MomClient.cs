using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks.Dataflow;
using NLog;

namespace Quantmom.Api
{
    public class MomClient
    {
        private readonly MomConnector _connector;
        public MomRspUserLogin? UserInfo;

        private static string GetLocalIPv4(NetworkInterfaceType interfaceType)
        {
            // Checks your IP address from the local network connected to a gateway. This to avoid issues with double network cards
            var output = "";  // default output
            foreach (var item in NetworkInterface.GetAllNetworkInterfaces()) // Iterate over each network interface
            {
                // Find the network interface which has been provided in the arguments, break the loop if found
                if (item.NetworkInterfaceType == interfaceType && item.OperationalStatus == OperationalStatus.Up)
                {
                    // Fetch the properties of this adapter
                    var adapterProperties = item.GetIPProperties();
                    // Check if the gateway address exist, if not its most likely a virtual network or smth
                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {
                        // Iterate over each available uni-cast addresses
                        foreach (var ip in adapterProperties.UnicastAddresses)
                        {
                            // If the IP is a local IPv4 address
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                // we got a match!
                                output = ip.Address.ToString();
                                break;  // break the loop!!
                            }
                        }
                    }
                }
                // Check if we got a result if so break this method
                if (output != "") { break; }
            }
            // Return results
            return output;
        }

        /// <summary>
        /// Finds the MAC address of the NIC with maximum speed.
        /// </summary>
        /// <returns>The MAC address.</returns>
        private static string GetMacAddress()
        {
            const int minMacAddressLength = 12;
            var macAddress = string.Empty;

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var tempMac = nic.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(tempMac)
                    && tempMac.Length >= minMacAddressLength)
                {
                    macAddress = tempMac;
                }
            }

            return macAddress;
        }

        public MomClient(string address, ILogger logger, bool debugMode = false)
            : this(new MomSocketConnector(address, logger, debugMode))
        {
        }

        public MomClient(MomConnector connector)
        {
            _connector = connector;
            var rspAction = new ActionBlock<MomResponse?>(ProcessResponse_);
            _connector.ReceiveTransformQueue.LinkTo(rspAction);
        }

        private void ProcessResponse_(MomResponse? rsp)
        {
            if (rsp == null)
            {
                return;
            }

            switch (rsp.MsgId)
            {
                case MomMessageType.Connected:
                    OnConnected?.Invoke();
                    break;
                case MomMessageType.Disconnected:
                    OnDisconnected?.Invoke(rsp.Data.IntValue);
                    break;
                case MomMessageType.RspUserLogin:
                    UserInfo = rsp.Data.AsRspUserLogin!;
                    OnRspUserLogin?.Invoke(UserInfo, rsp.RspInfo);
                    break;
                case MomMessageType.RtnNotice:
                    var notice = rsp.Data.AsNotice!;
                    UserInfo!.TradingDay = notice.TradingDay;
                    switch (notice.SystemStatus)
                    {
                        case MomSystemStatus.MarketClose:
                            OnMarketClose?.Invoke();
                            break;
                        case MomSystemStatus.Trading:
                            OnMarketOpen?.Invoke();
                            break;
                    }
                    ReturnNotice?.Invoke(notice);
                    break;
                case MomMessageType.RspError:
                    OnRspError?.Invoke(rsp.RspInfo);
                    break;

            }

            OnResponse?.Invoke(rsp);
            ProcessResponse(rsp);
        }

        protected virtual void ProcessResponse(MomResponse rsp)
        {
        }

        protected void Send(MomRequest request)
        {
            _connector.SendTransformQueue.Post(request);
        }

        public string Address => _connector.Address;
        public bool Connected => _connector.Connected;

        public event Action? OnConnected;
        public event Action<int>? OnDisconnected;
        public event Action<MomRspUserLogin, MomRspInfo>? OnRspUserLogin;
        public event Action<MomRspInfo>? OnRspError;
        public event Action<MomResponse>? OnResponse;
        public event Action? OnMarketClose;
        public event Action? OnMarketOpen;
        public event Action<MomNotice?>? ReturnNotice;

        public void Init()
        {
            _connector.SendInit();
            _connector.Connect();
        }

        public void Release()
        {
            _connector.Disconnect();
            OnDisconnected?.Invoke(0);
        }

        public void Login(string username, string password)
        {
            Login(new MomReqUserLogin
            {
                UserId = username, 
                Password = password,
                StrategyId = username,
                ClientIpAddress = GetLocalIPv4(NetworkInterfaceType.Ethernet),
                ClientMac = GetMacAddress()
            });
        }

        public void Login(MomReqUserLogin req)
        {
            req.ClientIpAddress = GetLocalIPv4(NetworkInterfaceType.Ethernet);
            req.ClientMac = GetMacAddress();
            Send(new MomRequest { MsgId = MomMessageType.UserLogin, Data = new MomAny(req) });
        }

        public void Logout(MomReqUserLogin req)
        {
            req.ClientIpAddress = GetLocalIPv4(NetworkInterfaceType.Ethernet);
            req.ClientMac = GetMacAddress();
            Send(new MomRequest { MsgId = MomMessageType.UserLogout, Data = new MomAny(req) });
        }
    }
}
