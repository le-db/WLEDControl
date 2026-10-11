using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace WLEDControl
{
    public class WLEDAdapter
    {
        public static async UniTask<bool> PostJson(string wledUrl, byte[] payload)
        {
            using var request = CreateRequest(wledUrl, RequestMethod.POST, payload);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Request failed: {request.error}");
                return false;
            }
            return true;
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
