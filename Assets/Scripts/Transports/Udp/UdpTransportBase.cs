using Cysharp.Threading.Tasks;
using System.Net.Sockets;
using Unity.Collections;
using UnityEngine;

namespace WLEDControl
{
    /// <summary>
    /// Owns the UDP socket. Frames are sent synchronously.
    /// </summary>
    public abstract class UdpTransportBase : ILedTransport
    {
        protected const int BytesPerLed = 3;

        readonly UdpClient _client;

        public abstract string Name { get; }

        protected UdpTransportBase(string host, int port)
        {
            _client = new UdpClient(host, port);
        }

        public UniTask<SendInfo> SendFrame(NativeArray<Color32> frame)
        {
            try
            {
                return UniTask.FromResult(SendPackets(frame));
            }
            catch (SocketException e)
            {
                Debug.LogError($"UDP send failed: {e.Message}");
                return UniTask.FromResult(new SendInfo(0, 0));
            }
        }

        protected abstract SendInfo SendPackets(NativeArray<Color32> frame);

        protected void SendDatagram(byte[] buffer, int length)
        {
            _client.Send(buffer, length);
        }

        protected static void WriteRgb(
            NativeArray<Color32> frame, int firstLed, int ledCount,
            byte[] buffer, int bufferOffset)
        {
            for (var i = 0; i < ledCount; i++)
            {
                var color = frame[firstLed + i];
                var offset = bufferOffset + i * BytesPerLed;

                buffer[offset] = color.r;
                buffer[offset + 1] = color.g;
                buffer[offset + 2] = color.b;
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
