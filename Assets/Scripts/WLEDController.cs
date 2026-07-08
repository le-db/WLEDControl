using Cysharp.Threading.Tasks;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace WLEDControl
{
    public class WLEDController : MonoBehaviour
    {
        [SerializeField] Camera myCamera;

        [SerializeField, Tooltip("X: base application target framerate\n" +
            "Y: refresh rate (e.g. refresh LEDs every 6th frame / 10Hz for 60fps)")] 
        int2 targetFrameAndRefreshRate = new(60, 6);
        float _refreshTimeoutSeconds;

        [SerializeField] int2 wledTextureSize = new(16, 16);
        NativeArray<Color32> _wledTextureData;
        
        [SerializeField] string wledUrl = "<WLED IP HERE>/json/";

        readonly StringBuilder _stringBuilder = new();

        void Start()
        {
            if (myCamera == null || myCamera.targetTexture == null)
            {
                Debug.LogError("Camera either null or has no target render texture");
                enabled = false;
            }

            Init();

            CustomUpdate(_refreshTimeoutSeconds, destroyCancellationToken).Forget();
        }

        void Init()
        {
            Application.targetFrameRate = targetFrameAndRefreshRate.x;
            _refreshTimeoutSeconds = (float)targetFrameAndRefreshRate.y / targetFrameAndRefreshRate.x;

            _wledTextureData = new NativeArray<Color32>(wledTextureSize.x * wledTextureSize.y, Allocator.Persistent);
        }

        async UniTaskVoid CustomUpdate(float timeout, CancellationToken ctk)
        {
            while (true)
            {
                await UniTask.NextFrame(ctk);
                await Send();
                await UniTask.WaitForSeconds(timeout, cancellationToken: ctk);
            }
        }

        [ContextMenu("Send")]
        async UniTask Send()
        {
            if (myCamera == null) return;
            if (!_wledTextureData.IsCreated) Init();

            // this throws when called multiple times a frame
            await AsyncGPUReadback.RequestIntoNativeArray(ref _wledTextureData, myCamera.targetTexture);

            await SendFrameToWLED(_wledTextureData);
        }

        void OnDestroy()
        {
            if (_wledTextureData.IsCreated) _wledTextureData.Dispose();
        }

        async UniTask SendFrameToWLED(NativeArray<Color32> frameData)
        {
            var payload = BuildPayload(frameData);
            if (payload == null || payload.Length == 0) return;

            using var request = new UnityWebRequest
            {
                url = wledUrl,
                method = "POST"
            };

            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogError($"Request failed: {request.error}");
        }

        byte[] BuildPayload(NativeArray<Color32> frameData)
        {
            _stringBuilder.Clear();

            _stringBuilder.Append("{\"seg\":[{\"id\":0,\"i\":[");
            for (int i = 0; i < frameData.Length; i++)
            {
                if (i > 0) _stringBuilder.Append(",");
                var color = frameData[i];

                string hexColor = $"{color.r:X2}{color.g:X2}{color.b:X2}";
                _stringBuilder.Append($"{i},\"{hexColor}\"");
            }
            _stringBuilder.Append("]}]}");

            return Encoding.UTF8.GetBytes(_stringBuilder.ToString());
        }
    }
}
