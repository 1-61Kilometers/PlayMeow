using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class MPU6050Controller : MonoBehaviour
{
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool threadRunning = false;
    
    // UDP port (must match the port in your Python script)
    private const int PORT = 5005;
    
    // Smoothing factor for the orientation
    [Range(0f, 0.99f)]
    public float smoothing = 0.5f;
    
    // Current smoothed values
    private Vector3 currentAcceleration = Vector3.zero;
    private Quaternion currentRotation = Quaternion.identity;
    private float currentTemp = 0f;
    
    [Header("Debug Information")]
    public bool showDebugInfo = true;
    public Vector3 debugAcceleration;
    public Vector3 debugEulerAngles;  // For debugging, we'll show Euler angles
    public Quaternion debugQuaternion;
    public float debugTemperature;
    public string lastReceivedData = "";
    public float timeSinceLastPacket = 0f;
    
    void Start()
    {
        Debug.Log($"Starting UDP client on port {PORT}");
        try
        {
            // Initialize UDP client
            udpClient = new UdpClient(PORT);
            udpClient.Client.ReceiveTimeout = 1000; // 1 second timeout
            
            // Start receive thread
            threadRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            Debug.Log("UDP receiver started successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting UDP client: {e}");
        }
    }
    
    private void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, PORT);
        
        while (threadRunning)
        {
            try
            {
                // Receive bytes
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string text = Encoding.UTF8.GetString(data);
                
                // Log raw received data and remote endpoint
                Debug.Log($"Received raw data from {remoteEndPoint.Address}:{remoteEndPoint.Port} - {text}");
                lastReceivedData = text;
                
                // Parse the values
                string[] values = text.Split(',');
                Debug.Log($"Split into {values.Length} values");
                
                // Check if we have all required values (3 accel + 1 temp + 4 quaternion = 8 values)
                if (values.Length == 11)  // Updated to match Python script output
                {
                    Vector3 newAccel = new Vector3(
                        float.Parse(values[0], CultureInfo.InvariantCulture),
                        float.Parse(values[1], CultureInfo.InvariantCulture),
                        float.Parse(values[2], CultureInfo.InvariantCulture)
                    );
                    
                    // Skip Euler angles (values[3,4,5]) as we're using quaternions now
                    float newTemp = float.Parse(values[6], CultureInfo.InvariantCulture);
                    
                    // Parse quaternion values (w, x, y, z)
                    Quaternion newRotation = new Quaternion(
                        float.Parse(values[7], CultureInfo.InvariantCulture),  // x
                        float.Parse(values[8], CultureInfo.InvariantCulture),  // y
                        float.Parse(values[9], CultureInfo.InvariantCulture), // z
                        float.Parse(values[10], CultureInfo.InvariantCulture)   // w
                    );
                    
                    // Log parsed values
                    Debug.Log($"Parsed values - Accel: {newAccel}, Quat: {newRotation}, Temp: {newTemp}");
                    
                    // Apply smoothing
                    currentAcceleration = Vector3.Lerp(currentAcceleration, newAccel, 1f - smoothing);
                    currentRotation = Quaternion.Slerp(currentRotation, newRotation, 1f - smoothing);
                    currentTemp = Mathf.Lerp(currentTemp, newTemp, 1f - smoothing);
                    
                    if (showDebugInfo)
                    {
                        debugAcceleration = currentAcceleration;
                        debugQuaternion = currentRotation;
                        debugEulerAngles = currentRotation.eulerAngles;  // Convert to Euler for debugging
                        debugTemperature = currentTemp;
                    }
                }
                else
                {
                    Debug.LogWarning($"Received wrong number of values: {values.Length} (expected 11)");
                }
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Socket timeout or error: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in receive thread: {e}");
            }
        }
    }
    
    void Update()
    {
        // Update time since last packet
        timeSinceLastPacket += Time.deltaTime;
        
        // Apply rotation using quaternion directly
        transform.rotation = currentRotation;
    }
    
    void OnDisable()
    {
        Debug.Log("Shutting down UDP client");
        threadRunning = false;
        if (receiveThread != null) 
        {
            try
            {
                receiveThread.Abort();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error shutting down thread: {e}");
            }
        }
        
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
                udpClient = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing UDP client: {e}");
            }
        }
    }
}