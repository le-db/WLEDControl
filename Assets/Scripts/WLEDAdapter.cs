using Cysharp.Threading.Tasks;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace WLEDControl
{
    public class WLEDAdapter
    {
        static readonly StringBuilder _stringBuilder = new();

        public static async UniTask PostFrame(NativeArray<Color32> frameData, string wledUrl)
        {
            var payload = BuildPayload(frameData);
            if (payload == null || payload.Length == 0) return;

            using var request = CreateRequest(wledUrl, RequestMethod.POST, payload);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogError($"Request failed: {request.error}");
        }

        public static async UniTask<string> GetStatus(string wledUrl)
        {
            using var request = CreateRequest(wledUrl);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Request failed: {request.error}");
                return string.Empty;
            }
            return request.downloadHandler.text;
        }

        static byte[] BuildPayload(NativeArray<Color32> frameData)
        {
            _stringBuilder.Clear();

            var segmentId = 0;
            _stringBuilder.AppendFormat("{0}{1}{2}",
                "{\"seg\":[{\"id\":",
                segmentId,
                ",\"i\":[");

            for (var i = 0; i < frameData.Length; i++)
            {
                if (i > 0) _stringBuilder.Append(",");
                var color = frameData[i];

                _stringBuilder.AppendFormat("{0},\"{1:X2}{2:X2}{3:X2}\"",
                    i, color.r, color.g, color.b);
            }

            _stringBuilder.Append("]}]}");

            return Encoding.UTF8.GetBytes(_stringBuilder.ToString());
        }

        static UnityWebRequest CreateRequest(
            string url,
            RequestMethod method = RequestMethod.GET,
            byte[] payload = null,
            string contentType = "application/json")
        {
            var request = new UnityWebRequest
            {
                url = url,
                method = method.ToString(),

                downloadHandler = new DownloadHandlerBuffer(),
            };

            bool canSendBody = method == RequestMethod.POST && payload != null;
            if (canSendBody)
                request.uploadHandler = new UploadHandlerRaw(payload);

            if (!string.IsNullOrEmpty(contentType))
                request.SetRequestHeader("Content-Type", contentType);

            return request;
        }

        enum RequestMethod
        {
            GET,
            POST,
        }
    }
}
