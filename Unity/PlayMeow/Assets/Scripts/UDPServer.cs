using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class SimpleUDPServer : MonoBehaviour
{
    [SerializeField] private int port = 12346;
    private UdpClient udpServer;
    private IPEndPoint endPoint;
    public GameObject targetObject;
    void Start()
    {
        try
        {
            udpServer = new UdpClient(port);
            endPoint = new IPEndPoint(IPAddress.Any, port);
            Debug.Log($"UDP server started on port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting server: {e.Message}");
        }
    }

    void Update()
    {
        if (udpServer != null && udpServer.Available > 0)
        {
            byte[] data = udpServer.Receive(ref endPoint);
            string message = Encoding.UTF8.GetString(data);
            Debug.Log(message);
            // Parse x,y from the received message (expected format: "x,y")
            string[] values = message.Split(',');
            if (values.Length == 2)
            {
                if (float.TryParse(values[0], out float x) && float.TryParse(values[1], out float y))
                {
                    // Update position directly
                    targetObject.transform.position = new Vector3(x, y, transform.position.z);
                    Debug.Log($"Received position: {transform.position}");
                }
            }
        }
    }

    void OnDestroy()
    {
        if (udpServer != null)
        {
            udpServer.Close();
        }
    }
}