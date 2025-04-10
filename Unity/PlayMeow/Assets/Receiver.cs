using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class Receiver : MonoBehaviour
{
    public GameObject joint1;  // First joint to control
    public GameObject joint2;  // Second joint to control
    
    private UdpClient listener;
    private int listenPort = 5005;  // Same port as the sender
    private bool isInitialized = false;
    private bool isQuitting = false;
    private Thread listenThread;
    
    // Queue for storing messages to process on the main thread
    private readonly Queue<string> messageQueue = new Queue<string>();
    private readonly Queue<string> errorQueue = new Queue<string>();
    private readonly object queueLock = new object();
    private readonly object errorLock = new object();

    public Sender sending;
    
    void Start()
    {
        try
        {
            // Initialize UDP listener
            listener = new UdpClient(listenPort);
            isInitialized = true;
            
            Debug.Log("UDP Listener initialized successfully on port " + listenPort);
            
            // Start listening for incoming messages in a separate thread
            listenThread = new Thread(new ThreadStart(ListenForMessages));
            listenThread.IsBackground = true;
            listenThread.Start();
        }
        catch (SocketException e)
        {
            Debug.LogError($"Failed to initialize UDP listener: {e.Message}");
        }
    }
    
    // Process messages from the queue on the main thread
    void Update()
    {
        if (!isInitialized && !isQuitting) return;
        
        // Process any error messages
        ProcessErrorMessages();
        
        // Process any UDP messages
        ProcessQueuedMessages();
    }
    
    private void ProcessErrorMessages()
    {
        List<string> errorsToProcess = new List<string>();
        
        lock (errorLock)
        {
            while (errorQueue.Count > 0)
            {
                errorsToProcess.Add(errorQueue.Dequeue());
            }
        }
        
        foreach (string error in errorsToProcess)
        {
            Debug.LogError(error);
        }
    }
    
    private void ProcessQueuedMessages()
    {
        List<string> messagesToProcess = new List<string>();
        
        lock (queueLock)
        {
            while (messageQueue.Count > 0)
            {
                messagesToProcess.Add(messageQueue.Dequeue());
            }
        }
        
        foreach (string message in messagesToProcess)
        {
            ProcessMessage(message);
        }
    }
    
    private void ListenForMessages()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        while (!isQuitting)
        {
            try
            {
                byte[] data = listener.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);
                
                // Add message to queue for processing in Update()
                lock (queueLock)
                {
                    messageQueue.Enqueue(message);
                }
            }
            catch (SocketException e)
            {
                // Check if we're quitting to avoid logging errors during shutdown
                if (!isQuitting)
                {
                    // Queue error for logging on main thread
                    lock (errorLock)
                    {
                        errorQueue.Enqueue($"Error receiving data: {e.Message}");
                    }
                    
                    // Brief pause before trying again to avoid tight loop on persistent error
                    Thread.Sleep(100);
                }
            }
            catch (ObjectDisposedException)
            {
                // Socket was closed, exit loop
                break;
            }
        }
    }
    
    private void ProcessMessage(string message)
    {
        try
        {
            // Parse the comma-separated angles
            string[] parts = message.Split(',');
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], out float servo0Angle) && 
                    float.TryParse(parts[1], out float servo1Angle))
                {
                    //sending.Send(servo0Angle, servo1Angle);
                    Debug.Log($"Robot - Joint1 Y: {servo0Angle}, Joint2 X: {servo1Angle}");
                    // For joint1, the angle mapping is straightforward
                    Vector3 joint1Rotation = joint1.transform.localEulerAngles;
                    joint1Rotation.z = servo0Angle;  // Reverse of the sender formula
                    joint1.transform.localEulerAngles = joint1Rotation;
                    
                    // For joint2, it's more complex due to the conditional logic in the sender
                    Vector3 joint2Rotation = joint2.transform.localEulerAngles;
                    
                    // Reverse the calculation from the sender
                    joint2Rotation.x = servo1Angle;  // Reverse of angle1 = (-joint2.transform.localEulerAngles.x + 90)
                    
                    joint2.transform.localEulerAngles = joint2Rotation;
                    
                    Debug.Log($"Applied angles - Joint1 Y: {joint1Rotation.z}, Joint2 X: {joint2Rotation.x}");
                }
                else
                {
                    Debug.LogWarning($"Received invalid angle format: {message}");
                }
            }
            else
            {
                Debug.LogWarning($"Received message with wrong format: {message}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
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
        isQuitting = true;
        
        // Close and dispose the listener
        if (listener != null)
        {
            try
            {
                listener.Close();
                listener.Dispose();
                listener = null;
            }
            catch (SocketException e)
            {
                Debug.LogError($"Error during cleanup: {e.Message}");
            }
        }
        
        // Wait for the thread to exit
        if (listenThread != null && listenThread.IsAlive)
        {
            listenThread.Join(1000);  // Wait up to 1 second for the thread to exit
            listenThread = null;
        }
    }
}