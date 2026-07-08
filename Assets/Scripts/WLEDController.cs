using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace WLEDControl
{
    public class WLEDController : MonoBehaviour
    {
        [SerializeField] Camera myCamera;

        [SerializeField, Tooltip("X: client application target framerate\n" +
            "Y: refresh rate (e.g. refresh LEDs every 6th frame / 10Hz for 60fps)")]
        int2 targetFrameAndRefreshRate = new(60, 6);
        float _refreshTimeoutSeconds;

        [SerializeField] int2 wledTextureSize = new(16, 16);
        NativeArray<Color32> wledTextureData;
        
        [SerializeField] string wledUrl = "<WLED IP HERE>/json/";

        void Start()
        {
            if (myCamera == null || myCamera.targetTexture == null)
            {
                Debug.LogError("Camera either null or has no target render texture");
                enabled = false;
                return;
            }

            Init();

            UpdateAsync(_refreshTimeoutSeconds, destroyCancellationToken).Forget();
        }

        void Init()
        {
            Application.targetFrameRate = targetFrameAndRefreshRate.x;
            _refreshTimeoutSeconds = (float)targetFrameAndRefreshRate.y / targetFrameAndRefreshRate.x;

            wledTextureData = new NativeArray<Color32>(wledTextureSize.x * wledTextureSize.y, Allocator.Persistent);
        }

        async UniTaskVoid UpdateAsync(float timeout, CancellationToken ctk)
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
            if (!wledTextureData.IsCreated) Init();

            // this throws when called multiple times a frame
            await AsyncGPUReadback.RequestIntoNativeArray(ref wledTextureData, myCamera.targetTexture);

            await WLEDAdapter.PostFrame(wledTextureData, wledUrl);
        }

        void OnDestroy()
        {
            if (wledTextureData.IsCreated) wledTextureData.Dispose();
        }
    }
}
