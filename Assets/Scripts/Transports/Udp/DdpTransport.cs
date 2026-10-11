using System;
using Unity.Collections;
using UnityEngine;

namespace WLEDControl
{
    /// <summary>
    /// Distributed Display Protocol: a 10 byte header with a byte offset into the pixel data.
    /// Only the last packet of a frame carries the push flag, so WLED shows the frame once complete.
    /// http://www.3waylabs.com/ddp/
    /// </summary>
    public class DdpTransport : UdpTransportBase
    {
        public const int DefaultPort = 4048;

        const int HeaderSize = 10;
        const int MaxLedsPerPacket = 480;
        const byte FlagsVersion1 = 0x40;
        const byte FlagPush = 0x01;
        const byte DataTypeRgb24 = 0x0B;
        const byte DestinationDisplay = 1;
        const int MaxSequence = 15;

        readonly byte[] _buffer = new byte[HeaderSize + MaxLedsPerPacket * BytesPerLed];
        // 1-15, 0 means "unused" in the protocol
        int _sequence;

        public override string Name => "UDP DDP";

        public DdpTransport(string host, int port = DefaultPort) : base(host, port) { }

        protected override SendInfo SendPackets(NativeArray<Color32> frame)
        {
            var bytes = 0;
            var packets = 0;
            _sequence = _sequence % MaxSequence + 1;

            for (var firstLed = 0; firstLed < frame.Length; firstLed += MaxLedsPerPacket)
            {
                var ledCount = Math.Min(MaxLedsPerPacket, frame.Length - firstLed);
                var isLastPacket = firstLed + ledCount >= frame.Length;

                WriteHeader(firstLed * BytesPerLed, ledCount * BytesPerLed, isLastPacket);
                WriteRgb(frame, firstLed, ledCount, _buffer, HeaderSize);

                var length = HeaderSize + ledCount * BytesPerLed;
                SendDatagram(_buffer, length);

                bytes += length;
                packets++;
            }

            return new SendInfo(bytes, packets);
        }

        void WriteHeader(int byteOffset, int dataLength, bool push)
        {
            _buffer[0] = push ? (byte)(FlagsVersion1 | FlagPush) : FlagsVersion1;
            _buffer[1] = (byte)_sequence;
            _buffer[2] = DataTypeRgb24;
            _buffer[3] = DestinationDisplay;
            _buffer[4] = (byte)(byteOffset >> 24);
            _buffer[5] = (byte)(byteOffset >> 16);
            _buffer[6] = (byte)(byteOffset >> 8);
            _buffer[7] = (byte)byteOffset;
            _buffer[8] = (byte)(dataLength >> 8);
            _buffer[9] = (byte)dataLength;
        }
    }
}
