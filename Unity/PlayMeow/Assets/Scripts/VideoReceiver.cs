using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;
using System;
using System.Net.WebSockets;

public class VideoReceiver : MonoBehaviour
{
    private NativeWebSocket.WebSocket websocket;
    private Texture2D videoTexture;
    public string serverUrl = "ws://73.40.115.122:8765";
    public Material targetMaterial;

    async void Start()
    {
        videoTexture = new Texture2D(640, 480);
        if (targetMaterial != null)
        {
            targetMaterial.mainTexture = videoTexture;
        }

        await ConnectToServer();
    }

    async Task ConnectToServer()
    {
        websocket = new NativeWebSocket.WebSocket(serverUrl);

        websocket.OnMessage += (bytes) =>
        {
            // Parse JSON message
            string jsonStr = System.Text.Encoding.UTF8.GetString(bytes);
            FrameData frameData = JsonUtility.FromJson<FrameData>(jsonStr);

            // Convert base64 to texture
            byte[] imageBytes = Convert.FromBase64String(frameData.data);
            videoTexture.LoadImage(imageBytes);
            videoTexture.Apply();
        };

        websocket.OnError += (string errMsg) =>
        {
            Debug.LogError($"WebSocket Error: {errMsg}");
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    async void OnDestroy()
    {
        if (websocket != null && websocket.State == NativeWebSocket.WebSocketState.Open)
        {
            await websocket.Close();
        }
    }
}

[Serializable]
public class FrameData
{
    public string type;
    public string data;
    public double timestamp;
}