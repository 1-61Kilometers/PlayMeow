using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;
using System.Runtime.InteropServices;

public class TrackerReciver : MonoBehaviour
{
    [Header("Network Settings")]
    public int tracker1Port = 8051;
    public int tracker2Port = 8052;
    public string localIP = "127.0.0.1";

    [Header("Tracked Objects")]
    public Transform tracker1Object;
    public Transform tracker2Object;

    private UdpClient udpClient1;
    private UdpClient udpClient2;
    private IPEndPoint endPoint1;
    private IPEndPoint endPoint2;
    private Thread receiveThread1;
    private Thread receiveThread2;
    private bool isRunning = true;

    private Vector3 tracker1Position = Vector3.zero;
    private Quaternion tracker1Rotation = Quaternion.identity;
    private Vector3 tracker2Position = Vector3.zero;
    private Quaternion tracker2Rotation = Quaternion.identity;
    private bool tracker1Updated = false;
    private bool tracker2Updated = false;

    void Start()
    {
        try
        {
            // Initialize UDP clients
            endPoint1 = new IPEndPoint(IPAddress.Parse(localIP), tracker1Port);
            udpClient1 = new UdpClient(tracker1Port);

            endPoint2 = new IPEndPoint(IPAddress.Parse(localIP), tracker2Port);
            udpClient2 = new UdpClient(tracker2Port);

            // Start receive threads
            receiveThread1 = new Thread(new ThreadStart(ReceiveTracker1Data));
            receiveThread1.IsBackground = true;
            receiveThread1.Start();

            receiveThread2 = new Thread(new ThreadStart(ReceiveTracker2Data));
            receiveThread2.IsBackground = true;
            receiveThread2.Start();

            Debug.Log("UDP clients initialized and listening.");
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP Socket error: {e}");
        }
    }

    private void ReceiveTracker1Data()
    {
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient1.Receive(ref endPoint1);
                ProcessTrackerData(data, ref tracker1Position, ref tracker1Rotation, ref tracker1Updated);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error receiving tracker 1 data: {e}");
            }
        }
    }

    private void ReceiveTracker2Data()
    {
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient2.Receive(ref endPoint2);
                ProcessTrackerData(data, ref tracker2Position, ref tracker2Rotation, ref tracker2Updated);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error receiving tracker 2 data: {e}");
            }
        }
    }

    private void ProcessTrackerData(byte[] data, ref Vector3 position, ref Quaternion rotation, ref bool updated)
    {
        // Data format: 7 doubles - x, y, z (position) and quaternion components
        if (data.Length == 7 * sizeof(double))
        {
            double[] values = new double[7];
            for (int i = 0; i < 7; i++)
            {
                values[i] = BitConverter.ToDouble(data, i * sizeof(double));
            }

            // Extract position
            position = new Vector3(-(float)values[0], (float)values[1], (float)values[2]);
            
            // Use the verified YWXZ quaternion configuration
            rotation = new Quaternion(
                (float)values[6], // Y
                (float)values[3], // W
                (float)values[4], // X
                (float)values[5]  // Z
            );
            
            updated = true;
        }
        else
        {
            Debug.LogWarning($"Received data with unexpected size: {data.Length} bytes");
        }
    }

    void Update()
    {
        // Update transforms with data from threads
        if (tracker1Updated && tracker1Object != null)
        {
            tracker1Object.position = tracker1Position;
            tracker1Object.rotation = tracker1Rotation;
            tracker1Updated = false;
        }

        if (tracker2Updated && tracker2Object != null)
        {
            tracker2Object.position = tracker2Position;
            tracker2Object.rotation = tracker2Rotation;
            tracker2Updated = false;
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        
        if (udpClient1 != null)
            udpClient1.Close();
        
        if (udpClient2 != null)
            udpClient2.Close();
    }
}
