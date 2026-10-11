using Cysharp.Threading.Tasks;
using System;
using Unity.Collections;
using UnityEngine;

namespace WLEDControl
{
    public interface ILedTransport : IDisposable
    {
        string Name { get; }

        UniTask<SendInfo> SendFrame(NativeArray<Color32> frame);
    }

    public readonly struct SendInfo
    {
        public readonly int Bytes;
        public readonly int Packets;

        public SendInfo(int bytes, int packets)
        {
            Bytes = bytes;
            Packets = packets;
        }
    }
}
