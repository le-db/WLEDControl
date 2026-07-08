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
        Texture2D _wledTexture;
        NativeArray<Color32> nativeColors;

        [SerializeField] int targetFrameRate = 60;
        [SerializeField] int refreshRate = 6;
        float _refreshTimeout;

        [SerializeField] int wledTextureMipLevel = 0;
        [SerializeField] int2 wledTextureSize = new(16, 16);
        Rect _wledTextureSizeReadRect;

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

            CustomUpdate(_refreshTimeout, destroyCancellationToken).Forget();
        }

        void Init()
        {
            Application.targetFrameRate = targetFrameRate;
            _refreshTimeout = (float) refreshRate / targetFrameRate;

            _wledTexture = new Texture2D(
                wledTextureSize.x, wledTextureSize.y,
                TextureFormat.RGBA32,
                mipChain: false, linear: false);

            nativeColors = new NativeArray<Color32>(wledTextureSize.x * wledTextureSize.y, Allocator.Persistent);

            _wledTextureSizeReadRect = new(0, 0, wledTextureSize.x, wledTextureSize.y);
        }

        async UniTask CustomUpdate(float timeout, CancellationToken ctk)
        {
            while (true)
            {
                await UniTask.NextFrame(ctk);
                Send();
                await UniTask.WaitForSeconds(timeout, cancellationToken: ctk);
            }
        }

        [ContextMenu("Send")]
        async void Send()
        {
            if (myCamera == null) return;
            if (_wledTexture == null) Init();

            //RenderTexture.active = myCamera.targetTexture;
            //_wledTexture.ReadPixels(_wledTextureSizeReadRect, 0, 0, recalculateMipMaps: false);
            //var nativeColors = _wledTexture.GetPixelData<Color32>(wledTextureMipLevel);
            //RenderTexture.active = null;

            // TODO: this throws when called every frame
            await AsyncGPUReadback.RequestIntoNativeArray(ref nativeColors, myCamera.targetTexture);

            await SendFrameToWLED(nativeColors);
        }

        void OnDestroy()
        {
            Destroy(_wledTexture);

            if (nativeColors.IsCreated) nativeColors.Dispose();
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
