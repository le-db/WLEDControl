using System;
using Unity.Collections;
using UnityEngine;

namespace WLEDControl
{
    /// <summary>
    /// WLED UDP realtime protocol 4 (DNRGB): a start index followed by RGB triplets.
    /// https://github.com/wled/WLED/wiki/UDP-Realtime-Control
    /// </summary>
    public class DnrgbTransport : UdpTransportBase
    {
        public const int DefaultPort = 21324;

        const int HeaderSize = 4;
        const int MaxLedsPerPacket = 489;
        const byte ProtocolDnrgb = 4;
        // WLED returns to normal mode this many seconds after the last packet
        const byte TimeoutSeconds = 2;

        readonly byte[] _buffer = new byte[HeaderSize + MaxLedsPerPacket * BytesPerLed];

        public override string Name => "UDP DNRGB";

        public DnrgbTransport(string host, int port = DefaultPort) : base(host, port) { }

        protected override SendInfo SendPackets(NativeArray<Color32> frame)
        {
            var bytes = 0;
            var packets = 0;

            for (var firstLed = 0; firstLed < frame.Length; firstLed += MaxLedsPerPacket)
            {
                var ledCount = Math.Min(MaxLedsPerPacket, frame.Length - firstLed);

                _buffer[0] = ProtocolDnrgb;
                _buffer[1] = TimeoutSeconds;
                _buffer[2] = (byte)(firstLed >> 8);
                _buffer[3] = (byte)firstLed;
                WriteRgb(frame, firstLed, ledCount, _buffer, HeaderSize);

                var length = HeaderSize + ledCount * BytesPerLed;
                SendDatagram(_buffer, length);

                bytes += length;
                packets++;
            }

            return new SendInfo(bytes, packets);
        }
    }
}
