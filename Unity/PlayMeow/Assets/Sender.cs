using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class Sender : MonoBehaviour
{
    public GameObject joint1;
    public GameObject joint2;
    private UdpClient client;
    private string serverIP = "10.0.0.21";
    private int port = 5005;
    private bool isInitialized = false;
    private bool isQuitting = false;

    async void Start()
    {
        try
        {
            client = new UdpClient();
            isInitialized = true;
            Debug.Log("UDP Client initialized successfully");
        }
        catch (SocketException e)
        {
            Debug.LogError($"Failed to initialize UDP client: {e.Message}");
        }
    }

    async void Update()
    {
        if (!isInitialized || isQuitting) return;

        float angle1 = (joint2.transform.localEulerAngles.x + 90);
        
        if ( angle1 > 270)
        {
            angle1 = angle1 -360;
        }
        float angle2 = (90 - joint1.transform.localEulerAngles.z);
        
        if(angle2 < 0){
            angle2 += 360;
        }

        float servo0Angle = Mathf.Clamp(angle2, 0, 180);
        float servo1Angle = Mathf.Clamp(angle1, 0, 180);
        
        string command = $"{servo0Angle},{servo1Angle}";
        
        byte[] data = Encoding.UTF8.GetBytes(command);

        try
        {
            await Task.Run(() =>
            {
                if (client != null && !isQuitting)
                {
                    client.Send(data, data.Length, serverIP, port);
                }
            });
        }
        catch (SocketException e)
        {
            Debug.LogError($"Failed to send data: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        CleanupNetwork();
    }

    void OnDestroy()
    {
        CleanupNetwork();
    }

    private void CleanupNetwork()
    {
        if (client != null)
        {
            try
            {
                client.Close();
                client.Dispose();
                client = null;
            }
            catch (SocketException e)
            {
                Debug.LogError($"Error during cleanup: {e.Message}");
            }
        }
    }
}