using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using ProtoBuf;
using Quantmom.Frontend;

// ReSharper disable InconsistentNaming

namespace Quantmom.Api
{
    internal static class ApiMessage
    {
        public const int MsgBufferSize = 1024 * 1024;
        public const int RequestOffset = 1;
        public const int ResponseOffset = 6;

        public static readonly MomResponse Connected;

        static ApiMessage()
        {
            Connected = new MomResponse
            {
                MsgId = MomMessageType.Connected,
                Last = true
            };
        }

        public static FrontendEvent MakeDisconnected(byte reason)
        {
            var e = new FrontendEvent();
            e.MsgData.Add(new[]
            {
                MomMessageType.Disconnected, BoolToByte(true),
                reason
            });
            e.MsgSize.Add(3);
            return e;
        }

        public static FrontendEvent MakeSignal(byte id, byte[]? clientId = null)
        {
            var e = new FrontendEvent(clientId);
            e.MsgData.Add(new[] { id });
            e.MsgSize.Add(1);
            return e;
        }

        public static FrontendEvent MakeInit(byte[]? identity = null)
        {
            return MakeSignal(MomMessageType.Init, identity);
        }

        public static FrontendEvent MakePing(byte[]? identity = null)
        {
            return MakeSignal(MomMessageType.Ping, identity);
        }

        public static FrontendEvent MakePong(byte[]? identity = null)
        {
            return MakeSignal(MomMessageType.Pong, identity);
        }

        public static FrontendEvent MakeClose(byte[]? identity = null, byte reason = 0)
        {
            var e = new FrontendEvent(identity);
            e.MsgData.Add(new[] { MomMessageType.Close, reason });
            e.MsgSize.Add(2);
            return e;
        }

        public static T Deserialize<T>(Stream stream)
        {
            var position = stream.Position;
            Serializer.TryReadLengthPrefix(stream, PrefixStyle.Base128, out var length);
            if (length == 0)
                return default!;
            stream.Position = position;
            return Serializer.DeserializeWithLengthPrefix<T>(stream, PrefixStyle.Base128);
        }

        public static void Serialize<T>(Stream stream, T data)
        {
            Serializer.SerializeWithLengthPrefix(stream, data, PrefixStyle.Base128);
        }

        public static void Serialize(Stream stream, ref MomDepthMarketData data)
        {
            Serializer.SerializeWithLengthPrefix(stream, data, PrefixStyle.Base128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte BoolToByte(bool v)
        {
            return (byte)(v ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ByteToBool(byte v)
        {
            return v == 1;
        }

        public static MomRequest EventToRequest(FrontendEvent e)
        {
            var msg = e.MsgData[0];
            var req = new MomRequest
            {
                Identity = e.Identity!,
                MsgId = msg[0]
            };

            var len = msg.Length - RequestOffset;
            if (len <= 0)
            {
                return req;
            }

            using var stream = new MemoryStream(msg, RequestOffset, len);
            switch (req.MsgId)
            {
                case MomMessageType.Close:
                    req.Data = new MomAny(msg[1]);
                    break;
                case MomMessageType.UserLogin:
                case MomMessageType.UserLogout:
                    req.Data = new MomAny(Deserialize<MomReqUserLogin>(stream));
                    break;
                case MomMessageType.Subscribe:
                case MomMessageType.Unsubscribe:
                    req.Data = new MomAny(Deserialize<string[]>(stream));
                    break;
                case MomMessageType.SubscribeResponse:
                    req.Data = new MomAny(Deserialize<MomSubscribeResponse>(stream));
                    break;
                case MomMessageType.InputOrder:
                    req.Data = new MomAny(Deserialize<MomInputOrder>(stream));
                    break;
                case MomMessageType.OrderAction:
                    req.Data = new MomAny(Deserialize<MomInputOrderAction>(stream));
                    break;
                case MomMessageType.DataAction:
                    req.Data = new MomAny(Deserialize<MomDataAction>(stream));
                    break;
                case MomMessageType.QryInstrument:
                    req.Data = new MomAny(Deserialize<MomQryInstrument>(stream));
                    break;
                case MomMessageType.QryUncheckInput:
                    req.Data = new MomAny(Deserialize<MomQryUncheckInput>(stream));
                    break;
                case MomMessageType.QryExchangeOrder:
                case MomMessageType.QryOrder:
                    req.Data = new MomAny(Deserialize<MomQryOrder>(stream));
                    break;
                case MomMessageType.QryTrade:
                    req.Data = new MomAny(Deserialize<MomQryTrade>(stream));
                    break;
                case MomMessageType.QryExchangeAccount:
                case MomMessageType.QryAccount:
                case MomMessageType.QryFundAccount:
                    req.Data = new MomAny(Deserialize<MomQryAccount>(stream));
                    break;
                case MomMessageType.QryExchangePosition:
                case MomMessageType.QryPosition:
                case MomMessageType.QryFundPosition:
                    req.Data = new MomAny(Deserialize<MomQryPosition>(stream));
                    break;
                case MomMessageType.QryPositionDetail:
                    req.Data = new MomAny(Deserialize<MomQryPositionDetail>(stream));
                    break;
                case MomMessageType.QryCashJournal:
                    req.Data = new MomAny(Deserialize<MomQryCashJournal>(stream));
                    break;
                case MomMessageType.QryRiskExpression:
                case MomMessageType.QryTradingChannel:
                case MomMessageType.QryTradingRoute:
                    req.Data = new MomAny(Deserialize<MomQryField>(stream));
                    break;
                case MomMessageType.QryPerformance:
                case MomMessageType.QryFundPerformance:
                    req.Data = new MomAny(Deserialize<MomQryPerformance>(stream));
                    break;
            }

            return req;
        }

        public static FrontendEvent RequestToEvent(MomRequest req)
        {
            var msgSize = RequestOffset;
            var buffer = new byte[MsgBufferSize];
            buffer[0] = req.MsgId;
            if (req.Data != MomAny.Empty)
            {
                using var stream = new MemoryStream(buffer, RequestOffset, buffer.Length - RequestOffset);
                switch (req.MsgId)
                {
                    case MomMessageType.UserLogin:
                    case MomMessageType.UserLogout:
                        Serialize(stream, req.Data.AsReqUserLogin);
                        break;
                    case MomMessageType.Subscribe:
                    case MomMessageType.Unsubscribe:
                        Serialize(stream, req.Data.StringArray);
                        break;
                    case MomMessageType.SubscribeResponse:
                        Serialize(stream, req.Data.AsSubscribeResponse);
                        break;
                    case MomMessageType.InputOrder:
                        Serialize(stream, req.Data.AsInputOrder);
                        break;
                    case MomMessageType.OrderAction:
                        Serialize(stream, req.Data.AsInputOrderAction);
                        break;
                    case MomMessageType.DataAction:
                        Serialize(stream, req.Data.AsDataAction);
                        break;
                    case MomMessageType.QryInstrument:
                        Serialize(stream, req.Data.AsQryInstrument);
                        break;
                    case MomMessageType.QryUncheckInput:
                        Serialize(stream, req.Data.AsQryUncheckInput);
                        break;
                    case MomMessageType.QryExchangeOrder:
                    case MomMessageType.QryOrder:
                        Serialize(stream, req.Data.AsQryOrder);
                        break;
                    case MomMessageType.QryTrade:
                        Serialize(stream, req.Data.AsQryTrade);
                        break;
                    case MomMessageType.QryExchangeAccount:
                    case MomMessageType.QryAccount:
                    case MomMessageType.QryFundAccount:
                        Serialize(stream, req.Data.AsQryAccount);
                        break;
                    case MomMessageType.QryExchangePosition:
                    case MomMessageType.QryPosition:
                    case MomMessageType.QryFundPosition:
                        Serialize(stream, req.Data.AsQryPosition);
                        break;
                    case MomMessageType.QryCashJournal:
                        Serialize(stream, req.Data.AsQryCashJournal);
                        break;
                    case MomMessageType.QryRiskExpression:
                    case MomMessageType.QryTradingChannel:
                    case MomMessageType.QryTradingRoute:
                        Serialize(stream, req.Data.AsQryField);
                        break;
                    case MomMessageType.QryPerformance:
                    case MomMessageType.QryFundPerformance:
                        Serialize(stream, req.Data.AsQryPerformance);
                        break;
                }
                msgSize += (int)stream.Position;
            }
            var e = new FrontendEvent();
            var msgData = new byte[msgSize];
            Array.Copy(buffer, msgData, msgData.Length);
            e.MsgData.Add(msgData);
            e.MsgSize.Add(msgData.Length);
            return e;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BytesToInt(IReadOnlyList<byte> bytes, int index)
        {
            var le = new UIntToByte
            {
                b3 = bytes[index + 0],
                b2 = bytes[index + 1],
                b1 = bytes[index + 2],
                b0 = bytes[index + 3]
            };
            return le.IntVal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IntToBytes(uint value, IList<byte> bytes, int index)
        {
            var le = new UIntToByte
            {
                IntVal = value
            };
            bytes[index + 0] = le.b3;
            bytes[index + 1] = le.b2;
            bytes[index + 2] = le.b1;
            bytes[index + 3] = le.b0;
        }

        public static MomResponse EventToResponse(FrontendEvent e)
        {
            var msg = e.MsgData[0];
            var rsp = new MomResponse
            {
                MsgId = msg[0],
                Last = ByteToBool(msg[1]),
                Index = BytesToInt(msg, 2)
            };
            var len = msg.Length - ResponseOffset;
            if (len <= 0)
            {
                return rsp;
            }

            using var stream = new MemoryStream(msg, ResponseOffset, len);
            switch (rsp.MsgId)
            {
                case MomMessageType.RspUserLogin:
                    rsp.Data = new MomAny(Deserialize<MomRspUserLogin>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspSubscribe:
                case MomMessageType.RspUnsubscribe:
                    rsp.Data = new MomAny(Deserialize<MomSpecificInstrument>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.Disconnected:
                    rsp.Data = new MomAny(stream.ReadByte());
                    break;
                case MomMessageType.RtnNotice:
                    rsp.Data = new MomAny(Deserialize<MomNotice>(stream));
                    break;
                case MomMessageType.RtnDepthMarketData:
                    var data = Deserialize<MomDepthMarketData>(stream);
                    rsp.Data = new MomAny(ref data);
                    break;
                case MomMessageType.RtnOrder:
                    rsp.Data = new MomAny(Deserialize<MomOrder>(stream));
                    break;
                case MomMessageType.RtnTrade:
                    rsp.Data = new MomAny(Deserialize<MomTrade>(stream));
                    break;
                case MomMessageType.RtnAccount:
                    rsp.Data = new MomAny(Deserialize<MomAccount>(stream));
                    break;
                case MomMessageType.RtnPosition:
                    rsp.Data = new MomAny(Deserialize<MomPosition>(stream));
                    break;
                case MomMessageType.RspError:
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryUncheckInput:
                case MomMessageType.RspInputOrder:
                case MomMessageType.RtnInputOrder:
                    rsp.Data = new MomAny(Deserialize<MomInputOrder>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RtnCheckOrder:
                    rsp.Data = new MomAny(Deserialize<MomInputOrder>(stream));
                    break;
                case MomMessageType.RspOrderAction:
                    rsp.Data = new MomAny(Deserialize<MomInputOrderAction>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspDataAction:
                    rsp.Data = new MomAny(Deserialize<MomDataAction>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspSubscribeResponse:
                    rsp.Data = new MomAny(Deserialize<MomSubscribeResponse>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryInstrument:
                    rsp.Data = new MomAny(Deserialize<MomInstrument>(stream));
                    break;
                case MomMessageType.RspQryUser:
                    rsp.Data = new MomAny(Deserialize<MomUser>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryOrder:
                    rsp.Data = new MomAny(Deserialize<MomOrder>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryTrade:
                    rsp.Data = new MomAny(Deserialize<MomTrade>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryAccount:
                    rsp.Data = new MomAny(Deserialize<MomAccount>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryPosition:
                    rsp.Data = new MomAny(Deserialize<MomPosition>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryFund:
                    rsp.Data = new MomAny(Deserialize<MomFund>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryExchangeAccount:
                case MomMessageType.RspQryFundAccount:
                    rsp.Data = new MomAny(Deserialize<MomFundAccount>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryExchangePosition:
                case MomMessageType.RspQryFundPosition:
                    rsp.Data = new MomAny(Deserialize<MomFundPosition>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RspQryExchangeOrder:
                    rsp.Data = new MomAny(Deserialize<MomFundOrder>(stream));
                    rsp.RspInfo = Deserialize<MomRspInfo>(stream);
                    break;
                case MomMessageType.RtnFundAccount:
                    rsp.Data = new MomAny(Deserialize<MomFundAccount>(stream));
                    break;
                case MomMessageType.RtnFundPosition:
                    rsp.Data = new MomAny(Deserialize<MomFundPosition>(stream));
                    break;
                case MomMessageType.RspQryTradingChannel:
                    rsp.Data = new MomAny(Deserialize<MomTradingChannel>(stream));
                    break;
                case MomMessageType.RspQryTradingRoute:
                    rsp.Data = new MomAny(Deserialize<MomTradingRoute>(stream));
                    break;
                case MomMessageType.RspQryRiskExpression:
                    rsp.Data = new MomAny(Deserialize<MomRiskExpression>(stream));
                    break;
                case MomMessageType.RspQryPerformance:
                    rsp.Data = new MomAny(Deserialize<MomPerformance>(stream));
                    break;
                case MomMessageType.RspQryFundPerformance:
                    rsp.Data = new MomAny(Deserialize<MomFundPerformance>(stream));
                    break;
            }

            return rsp;
        }

        public static FrontendEvent ResponseToEvent(MomResponse rsp)
        {
            var buffer = new byte[MsgBufferSize];
            buffer[0] = rsp.MsgId;
            buffer[1] = BoolToByte(rsp.Last);
            IntToBytes(rsp.Index, buffer, 2);
            using var stream = new MemoryStream(buffer, ResponseOffset, buffer.Length - ResponseOffset);
            switch (rsp.MsgId)
            {
                case MomMessageType.RtnDepthMarketData:
                    {
                        rsp.Data.GetMarketData(out var data);
                        Serialize(stream, ref data);
                    }
                    break;
                case MomMessageType.RtnNotice:
                    Serialize(stream, rsp.Data.AsNotice);
                    break;
                case MomMessageType.RtnOrder:
                    Serialize(stream, rsp.Data.AsOrder);
                    break;
                case MomMessageType.RtnTrade:
                    Serialize(stream, rsp.Data.AsTrade);
                    break;
                case MomMessageType.RtnAccount:
                    Serialize(stream, rsp.Data.AsAccount);
                    break;
                case MomMessageType.RtnPosition:
                    Serialize(stream, rsp.Data.AsPosition);
                    break;
                case MomMessageType.RspUserLogin:
                    Serialize(stream, rsp.Data.AsRspUserLogin);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspSubscribe:
                case MomMessageType.RspUnsubscribe:
                    Serialize(stream, rsp.Data.AsSpecificInstrument);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspError:
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryInstrument:
                    Serialize(stream, rsp.Data.AsInstrument);
                    break;
                case MomMessageType.RspQryUser:
                    Serialize(stream, rsp.Data.AsUser);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryUncheckInput:
                case MomMessageType.RspInputOrder:
                case MomMessageType.RtnInputOrder:
                    Serialize(stream, rsp.Data.AsInputOrder);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RtnCheckOrder:
                    Serialize(stream, rsp.Data.AsInputOrder);
                    break;
                case MomMessageType.RspOrderAction:
                    Serialize(stream, rsp.Data.AsInputOrderAction);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspDataAction:
                    Serialize(stream, rsp.Data.AsDataAction);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryOrder:
                    Serialize(stream, rsp.Data.AsOrder);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryTrade:
                    Serialize(stream, rsp.Data.AsTrade);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryAccount:
                    Serialize(stream, rsp.Data.AsAccount);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryPosition:
                    Serialize(stream, rsp.Data.AsPosition);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryPositionDetail:
                    Serialize(stream, rsp.Data.AsPositionDetail);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryFund:
                    Serialize(stream, rsp.Data.AsFund);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryFundAccount:
                    Serialize(stream, rsp.Data.AsFundAccount);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryFundPosition:
                    Serialize(stream, rsp.Data.AsFundPosition);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RtnFundAccount:
                    Serialize(stream, rsp.Data.AsFundAccount);
                    break;
                case MomMessageType.RtnFundPosition:
                    Serialize(stream, rsp.Data.AsFundPosition);
                    break;
                case MomMessageType.RspQryCashJournal:
                    Serialize(stream, rsp.Data.AsCashJournal);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspSubscribeResponse:
                    Serialize(stream, rsp.Data.AsSubscribeResponse);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryTradingChannel:
                    Serialize(stream, rsp.Data.AsTradingChannel);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryTradingRoute:
                    Serialize(stream, rsp.Data.AsTradingRoute);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryRiskExpression:
                    Serialize(stream, rsp.Data.AsRiskExpression);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryPerformance:
                    Serialize(stream, rsp.Data.AsPerformance);
                    Serialize(stream, rsp.RspInfo);
                    break;
                case MomMessageType.RspQryFundPerformance:
                    Serialize(stream, rsp.Data.AsFundPerformance);
                    Serialize(stream, rsp.RspInfo);
                    break;
            }

            var e = new FrontendEvent(rsp.Identity);
            var msgData = new byte[(int)stream.Position + ResponseOffset];
            Array.Copy(buffer, msgData, msgData.Length);
            e.MsgData.Add(msgData);
            e.MsgSize.Add(msgData.Length);
            return e;
        }
    }
}
