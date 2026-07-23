using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

        [SerializeField] string wledUrl = "<WLED IP HERE>/json/";

        NativeArray<Color32> wledTextureData;
        string wledTextureAssetPath = "Assets/Scripts/CameraTarget.renderTexture";

        async void Start()
        {
            if (myCamera == null)
            {
                Debug.LogError("Camera is null");
                enabled = false;
                return;
            }

            await Init();

            UpdateAsync(destroyCancellationToken).Forget();
        }

        async UniTask Init()
        {
            var startTime = DateTime.UtcNow;

            Application.targetFrameRate = targetFrameAndRefreshRate.x;
            _refreshTimeoutSeconds = (float)targetFrameAndRefreshRate.y / targetFrameAndRefreshRate.x;

            var textureSize = await GetDimensions();
            var targetTexture = new RenderTexture(textureSize.x, textureSize.y, depth: 32, GraphicsFormat.R8G8B8A8_UNorm)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,

                useMipMap = true,
                autoGenerateMips = true,
            };

            myCamera.targetTexture = targetTexture;

#if UNITY_EDITOR
            if (AssetDatabase.AssetPathExists(wledTextureAssetPath))
                AssetDatabase.DeleteAsset(wledTextureAssetPath);
            AssetDatabase.CreateAsset(targetTexture, wledTextureAssetPath);
#endif

            wledTextureData = new NativeArray<Color32>(textureSize.x * textureSize.y, Allocator.Persistent);

            Debug.Log($"Controller initialized in {(startTime - DateTime.UtcNow).TotalMilliseconds}ms");
        }

        async UniTask<int2> GetDimensions()
        {
            var statusJson = await WLEDAdapter.GetStatus(wledUrl);

            var statusDic = JsonConvert.DeserializeObject<Dictionary<string, object>>(statusJson);
            var stateDic = JsonConvert.DeserializeObject<Dictionary<string, object>>(statusDic["state"].ToString());
            var segments = JsonConvert.DeserializeObject<object[]>(stateDic["seg"].ToString());

            var sizeX = 0;
            var sizeY = 0;

            foreach (var seg in segments)
            {
                var segDic = JsonConvert.DeserializeObject<Dictionary<string, object>>(seg.ToString());
                
                var startX = int.Parse(segDic["start"].ToString());
                var stopX  = int.Parse(segDic["stop"].ToString());
                var startY = int.Parse(segDic["startY"].ToString());
                var stopY  = int.Parse(segDic["stopY"].ToString());

                sizeX += stopX - startX;
                sizeY += stopY - startY;
            }

            return new(sizeX, sizeY);
        }

        async UniTaskVoid UpdateAsync(CancellationToken ctk)
        {
            while (true)
            {
                await UniTask.NextFrame(ctk);
                // TODO: UDP live mode
                // https://github.com/wled/WLED/wiki/UDP-Realtime-Control
                // https://learn.microsoft.com/de-de/dotnet/api/system.net.sockets.udpclient.send?view=net-10.0
                await Send();
                await UniTask.WaitForSeconds(_refreshTimeoutSeconds, cancellationToken: ctk);
            }
        }

        [ContextMenu("Send")]
        async UniTask Send()
        {
            if (myCamera == null) return;
            if (!wledTextureData.IsCreated) await Init();

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
