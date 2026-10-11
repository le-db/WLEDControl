using Cysharp.Threading.Tasks;
using System;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace WLEDControl
{
    /// <summary>
    /// Sends frames through the WLED JSON API, as segment 0 individual LED control.
    /// WLED recommends at most 256 colors per call and no parallel calls, so a frame is split into sequential requests.
    /// </summary>
    public class HttpJsonTransport : ILedTransport
    {
        const int MaxLedsPerRequest = 256;

        readonly StringBuilder _stringBuilder = new();
        readonly string _jsonUrl;

        public string Name => "HTTP JSON";

        public HttpJsonTransport(string jsonUrl)
        {
            _jsonUrl = jsonUrl;
        }

        public async UniTask<SendInfo> SendFrame(NativeArray<Color32> frame)
        {
            var bytes = 0;
            var requests = 0;

            for (var firstLed = 0; firstLed < frame.Length; firstLed += MaxLedsPerRequest)
            {
                var ledCount = Math.Min(MaxLedsPerRequest, frame.Length - firstLed);
                var payload = BuildPayload(frame, firstLed, ledCount);

                var success = await WLEDAdapter.PostJson(_jsonUrl, payload);
                if (!success) break;

                bytes += payload.Length;
                requests++;
            }

            return new SendInfo(bytes, requests);
        }

        byte[] BuildPayload(NativeArray<Color32> frame, int firstLed, int ledCount)
        {
            var segmentId = 0;

            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{{\"seg\":[{{\"id\":{0},\"i\":[{1}", segmentId, firstLed);

            for (var i = firstLed; i < firstLed + ledCount; i++)
            {
                var color = frame[i];
                _stringBuilder.AppendFormat(",\"{0:X2}{1:X2}{2:X2}\"", color.r, color.g, color.b);
            }

            _stringBuilder.Append("]}]}");

            return Encoding.UTF8.GetBytes(_stringBuilder.ToString());
        }

        public void Dispose() { }
    }
}
