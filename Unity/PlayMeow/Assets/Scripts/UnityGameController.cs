using UnityEngine;
using System;
using System.Collections;
using NativeWebSocket;
using System.Threading.Tasks;

[Serializable]
public class PositionData
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class WebSocketMessage
{
    public PositionData position;
}

public class UnityGameController : MonoBehaviour
{
    [SerializeField] private GameObject controlledObject;
    [SerializeField] private string webSocketUrl = "ws://73.40.115.122:8765";
    [SerializeField] private float movementSmoothing = 0.1f;
    
    private WebSocket webSocket;
    private Vector3 targetPosition;

    async void Start()
    {
        targetPosition = controlledObject.transform.position;
        await StartWebSocketServer();
    }

    async Task StartWebSocketServer()
    {
        webSocket = new WebSocket(webSocketUrl);

        webSocket.OnOpen += () =>
        {
            Debug.Log("WebSocket server started and listening on: " + webSocketUrl);
        };

        webSocket.OnError += (e) =>
        {
            Debug.LogError($"WebSocket error: {e}");
        };

        webSocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket connection closed");
        };

        webSocket.OnMessage += (bytes) =>
        {
            string message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log($"Received message: {message}");
            
            try
            {
                if (message.Contains("position"))
                {
                    WebSocketMessage posData = JsonUtility.FromJson<WebSocketMessage>(message);
                    if (posData != null && posData.position != null)
                    {
                        targetPosition = new Vector3(posData.position.x, posData.position.y, posData.position.z);
                        Debug.Log($"Position updated: {targetPosition}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing WebSocket message: {ex.Message}");
            }
        };

        await webSocket.Connect();
        Debug.Log("WebSocket server started successfully");
    }

    void Update()
    {
        // Process WebSocket messages
        #if !UNITY_WEBGL || UNITY_EDITOR
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            webSocket.DispatchMessageQueue();
        }
        #endif
        
        // Smoothly move the controlled object to the target position
        if (controlledObject != null)
        {
            controlledObject.transform.position = Vector3.Lerp(
                controlledObject.transform.position, 
                targetPosition, 
                movementSmoothing
            );
        }
    }

    async void OnDestroy()
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            await webSocket.Close();
        }
    }
}