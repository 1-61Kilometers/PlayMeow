using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine.Events;

/// <summary>
/// PlayMeow Laser Controller with Direct Control Mode
/// 
/// Provides controls for moving a laser pointer in the scene.
/// Modified to connect to the Python backend for direct control.
/// </summary>
public class PlayMeowLaserController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed when using keyboard")]
    public float moveSpeed = 3.0f;
    
    [Tooltip("Whether to constrain the laser to the play area bounds")]
    public bool constrainToPlayArea = true;

    [Tooltip("Play area boundaries [minX, maxX, minZ, maxZ]")]
    public Vector4 playAreaBounds = new Vector4(-2f, 2f, -2f, 2f);
    
    [Tooltip("Reference to the cat GameObject for position tracking")]
    public GameObject catObject;

    [Header("Auto Movement")]
    [Tooltip("Whether to enable automatic movement patterns")]
    public bool enableAutoMovement = false;

    [Tooltip("Movement pattern to use")]
    public MovementPattern pattern = MovementPattern.CircularPattern;

    [Tooltip("Speed of automatic movement")]
    public float autoMoveSpeed = 1.0f;

    [Header("Direct Control Settings")]
    [Tooltip("Whether to enable direct control mode with Python backend")]
    public bool enableDirectControl = false;

    [Tooltip("Host for connection to Python backend")]
    public string serverHost = "localhost";

    [Tooltip("Port for connection to Python backend")]
    public int serverPort = 12345;

    [Tooltip("How often to send data to Python backend (seconds)")]
    public float updateInterval = 0.1f;
    
    [Tooltip("How often to send keep-alive pings (seconds)")]
    public float pingInterval = 2.0f;

    [Tooltip("Event fired when connection status changes")]
    public UnityEvent<bool> onConnectionChanged;

    // Enum for different movement patterns
    public enum MovementPattern
    {
        CircularPattern,
        FigureEightPattern,
        RandomPattern,
        SineWavePattern
    }

    // Internal variables
    private Vector3 initialPosition;
    private float autoMoveTimer = 0f;
    private Vector3 randomTargetPos;
    private float randomTargetTimer = 0f;
    
    // Network-related variables
    private TcpClient socketConnection;
    private Thread clientReceiveThread;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private float lastUpdateTime = 0f;
    private float lastPingTime = 0f;
    private bool isConnected = false;
    private Vector3 externalMoveDirection = Vector3.zero;
    private bool isListening = false;

    void Start()
    {
        initialPosition = transform.position;
        
        // Initialize random target if using random pattern
        if (pattern == MovementPattern.RandomPattern)
        {
            SetNewRandomTarget();
        }
        
        // Connect to Python backend if direct control is enabled
        if (enableDirectControl)
        {
            ConnectToServer();
        }
    }
    
    void OnDestroy()
    {
        // Clean up socket connection when destroyed
        CloseConnection();
    }

    void Update()
    {
        // Process messages from Python backend
        ProcessMessageQueue();
        
        // Determine movement type based on settings
        if (enableDirectControl && isConnected)
        {
            // Move based on data from Python backend
            MoveWithDirectControl();
            
            // Send updates to Python backend periodically
            SendUpdateToPython();
            
            // Send regular pings to keep the connection alive
            SendPingIfNeeded();
        }
        else if (enableAutoMovement)
        {
            MoveAutomatically();
        }
        else
        {
            MoveWithInput();
        }

        // Constrain to play area if enabled
        if (constrainToPlayArea)
        {
            ConstrainToPlayArea();
        }
    }
    
    /// <summary>
    /// Move the laser based on external control from Python backend
    /// </summary>
    private void MoveWithDirectControl()
    {
        // Apply movement from Python if available
        if (externalMoveDirection != Vector3.zero)
        {
            transform.position += externalMoveDirection * moveSpeed * Time.deltaTime;
        }
    }
    
    /// <summary>
    /// Connect to the Python backend server
    /// </summary>
    private void ConnectToServer()
    {
        try
        {
            // Close any existing connections
            CloseConnection();
            
            // Reset ping timer
            lastPingTime = Time.time;
            
            // Validate that the cat reference is set
            if (catObject == null)
            {
                Debug.LogWarning("No cat object reference set. Cat position data will not be sent to Python backend.");
            }
            
            // Create new TCP client
            socketConnection = new TcpClient();
            socketConnection.NoDelay = true;
            socketConnection.SendTimeout = 15000; // 15 second timeout for sends (increased from 5)
            socketConnection.ReceiveTimeout = 15000; // 15 second timeout for receives (increased from 5)
            
            // Set keep-alive option to prevent the connection from dropping
            // Unity's .NET implementation might not support all TCP keep-alive options
            socketConnection.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            
            socketConnection.Connect(serverHost, serverPort);
            
            // Create thread for receiving data
            isListening = true;
            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
            
            // Update connection status
            isConnected = true;
            
            // Trigger connection event
            if (onConnectionChanged != null)
                onConnectionChanged.Invoke(true);
                
            Debug.Log($"Connected to Python backend at {serverHost}:{serverPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
            isConnected = false;
            
            // Trigger connection event
            if (onConnectionChanged != null)
                onConnectionChanged.Invoke(false);
        }
    }
    
    /// <summary>
    /// Close the connection to the Python backend
    /// </summary>
    private void CloseConnection()
    {
        try
        {
            // Try to send a disconnect command before closing
            if (socketConnection != null && socketConnection.Connected)
            {
                try
                {
                    // Create a disconnect message
                    PythonUpdate disconnectMsg = new PythonUpdate
                    {
                        laserX = transform.position.x,
                        laserZ = transform.position.z,
                        laserY = transform.position.y,
                        timestamp = Time.time,
                        command = "disconnect"
                    };
                    
                    // Convert to JSON
                    string jsonMessage = JsonUtility.ToJson(disconnectMsg);
                    
                    // Send disconnect message
                    NetworkStream stream = socketConnection.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
                    stream.Write(data, 0, data.Length);
                    
                    // Brief pause to allow server to process
                    Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error sending disconnect message: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error in disconnect preparation: {e.Message}");
        }
        
        // Signal the receiving thread to stop
        isListening = false;
        
        // Wait briefly for thread to detect the signal
        if (clientReceiveThread != null && clientReceiveThread.IsAlive)
        {
            try
            {
                // Don't wait too long to avoid blocking
                clientReceiveThread.Join(1000);  // Increased from 500ms to 1000ms
                
                // Only abort the thread if it's still running after the graceful shutdown attempt
                if (clientReceiveThread.IsAlive)
                {
                    try
                    {
                        clientReceiveThread.Abort();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Error aborting thread: {e.Message}");
                    }
                }
                
                clientReceiveThread = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing thread: {e.Message}");
            }
        }
        
        // Close the socket connection
        if (socketConnection != null)
        {
            try
            {
                if (socketConnection.Connected)
                {
                    // Close socket properly
                    socketConnection.GetStream().Close();
                    socketConnection.Close();
                    Debug.Log("Disconnected from Python backend");
                }
                socketConnection = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing socket: {e.Message}");
            }
        }
        
        // Update connection status
        if (isConnected)
        {
            isConnected = false;
            
            // Trigger connection event
            if (onConnectionChanged != null)
                onConnectionChanged.Invoke(false);
        }
    }
    
    /// <summary>
    /// Listen for data from Python backend in a separate thread
    /// </summary>
    private void ListenForData()
    {
        byte[] buffer = new byte[4096];
        NetworkStream stream = null;
        
        try
        {
            Debug.Log("Listener thread started");
            
            // Get the network stream once outside the loop
            if (socketConnection != null && socketConnection.Connected)
            {
                stream = socketConnection.GetStream();
            }
            else
            {
                Debug.LogError("Unable to get network stream - not connected");
                isListening = false;
            }
            
            // Main listener loop
            while (isListening && socketConnection != null && socketConnection.Connected && stream != null)
            {
                try
                {
                    // Check if the socket is still connected
                    if (!socketConnection.Connected)
                    {
                        Debug.Log("Socket disconnected, exiting listener loop");
                        break;
                    }
                    
                    // Check if data is available before trying to read
                    if (socketConnection.Available > 0 || stream.DataAvailable)
                    {
                        int length = stream.Read(buffer, 0, buffer.Length);
                        
                        if (length > 0)
                        {
                            // Convert data to string
                            string message = Encoding.UTF8.GetString(buffer, 0, length);
                            
                            // Queue message for processing in main thread
                            messageQueue.Enqueue(message);
                        }
                    }
                    else
                    {
                        // No data available, sleep briefly to avoid CPU spinning
                        Thread.Sleep(10);
                    }
                }
                catch (System.IO.IOException ioEx)
                {
                    // Handle IO exceptions which may occur during normal operation
                    if (isListening)
                    {
                        Debug.LogWarning($"IO exception in listener: {ioEx.Message}");
                    }
                    Thread.Sleep(100);
                }
                catch (SocketException sockEx)
                {
                    // Socket errors typically mean the connection is broken
                    Debug.LogWarning($"Socket exception in listener: {sockEx.Message}");
                    break;
                }
            }
        }
        catch (ThreadAbortException)
        {
            // Thread is being aborted - this is expected during cleanup
            Debug.Log("Listener thread aborted");
        }
        catch (Exception e)
        {
            // Only log errors if we're still supposed to be listening
            if (isListening)
            {
                Debug.LogError($"Error receiving data: {e.Message}");
            }
        }
        finally
        {
            // Clean up
            isListening = false;
            Debug.Log("Listener thread exiting");
        }
    }
    
    /// <summary>
    /// Process messages from Python backend in the main thread
    /// </summary>
    private void ProcessMessageQueue()
    {
        string message;
        while (messageQueue.TryDequeue(out message))
        {
            try
            {
                // Parse JSON response
                var response = JsonUtility.FromJson<PythonResponse>(message);
                
                if (response != null && response.status == "success")
                {
                    // Apply movement values from Python
                    externalMoveDirection = new Vector3(response.moveX, 0, response.moveZ);
                }
                else if (response != null && response.status == "ping")
                {
                    // Just a ping to keep the connection alive, do nothing
                }
                else if (response != null && (response.status == "paused" || response.status == "resumed"))
                {
                    // Handle pause/resume status changes
                    bool isPaused = response.status == "paused" || response.paused;
                    externalMoveDirection = Vector3.zero; // Stop movement when paused
                    
                    // Notify any listeners about the status change
                    if (onConnectionChanged != null)
                    {
                        // We're still connected, but status changed
                        onConnectionChanged.Invoke(!isPaused);
                    }
                    
                    // Log the status change
                    Debug.Log(isPaused ? "Laser movement paused" : "Laser movement resumed");
                }
                else
                {
                    Debug.LogWarning($"Invalid or error response: {message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing message: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Send update to Python backend with current state
    /// </summary>
    private void SendUpdateToPython()
    {
        // Send updates at the specified interval
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = Time.time;
            
            if (socketConnection != null && socketConnection.Connected)
            {
                // Get keyboard input for direct control
                float horizontalInput = Input.GetAxis("Horizontal");
                float verticalInput = Input.GetAxis("Vertical");
                
                // Create message with control values
                PythonUpdate update = new PythonUpdate
                {
                    laserX = transform.position.x,
                    laserZ = transform.position.z,
                    laserY = transform.position.y,
                    controlX = horizontalInput,
                    controlZ = verticalInput,
                    timestamp = Time.time,
                    bounds = new float[] { playAreaBounds.x, playAreaBounds.y, playAreaBounds.z, playAreaBounds.w },
                    // Add heartbeat field to indicate if we need to stay connected even when not moving
                    heartbeat = Time.time
                };
                
                // Add cat position data if available
                if (catObject != null)
                {
                    Vector3 catPosition = catObject.transform.position;
                    update.catX = catPosition.x;
                    update.catY = catPosition.y;
                    update.catZ = catPosition.z;
                    
                    // Try to get velocity from Rigidbody if present
                    Rigidbody catRigidbody = catObject.GetComponent<Rigidbody>();
                    if (catRigidbody != null)
                    {
                        Vector3 velocity = catRigidbody.linearVelocity;
                        update.catVelX = velocity.x;
                        update.catVelZ = velocity.z;
                    }
                }
                
                // Convert to JSON
                string jsonMessage = JsonUtility.ToJson(update);
                
                try
                {
                    // Send data to server - reuse the existing stream instead of creating a new one
                    NetworkStream stream = socketConnection.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
                    stream.Write(data, 0, data.Length);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error sending data: {e.Message}");
                    
                    // Add retry logic instead of immediately closing the connection
                    if (e is SocketException || e is System.IO.IOException)
                    {
                        // Try to reconnect on the next update rather than closing immediately
                        isConnected = false;
                    }
                    else
                    {
                        // Only close connection for other types of exceptions
                        CloseConnection();
                    }
                }
            }
            else if (enableDirectControl && !isConnected)
            {
                // Try to reconnect if we lost connection
                ConnectToServer();
            }
        }
    }
    
    /// <summary>
    /// Send a pause command to the Python backend
    /// </summary>
    public void PauseMovement()
    {
        if (socketConnection != null && socketConnection.Connected)
        {
            SendCommandToPython("pause");
        }
    }
    
    /// <summary>
    /// Send a resume command to the Python backend
    /// </summary>
    public void ResumeMovement()
    {
        if (socketConnection != null && socketConnection.Connected)
        {
            SendCommandToPython("resume");
        }
    }
    
    /// <summary>
    /// Send a command to the Python backend
    /// </summary>
    private void SendCommandToPython(string command)
    {
        if (socketConnection == null || !socketConnection.Connected)
            return;
            
        try
        {
            // Create command message
            PythonUpdate commandMsg = new PythonUpdate
            {
                laserX = transform.position.x,
                laserZ = transform.position.z,
                laserY = transform.position.y,
                timestamp = Time.time,
                heartbeat = Time.time,
                command = command
            };
            
            // Add cat position data if available
            if (catObject != null)
            {
                Vector3 catPosition = catObject.transform.position;
                commandMsg.catX = catPosition.x;
                commandMsg.catY = catPosition.y;
                commandMsg.catZ = catPosition.z;
                
                // Try to get velocity if available
                Rigidbody catRigidbody = catObject.GetComponent<Rigidbody>();
                if (catRigidbody != null)
                {
                    Vector3 velocity = catRigidbody.linearVelocity;
                    commandMsg.catVelX = velocity.x;
                    commandMsg.catVelZ = velocity.z;
                }
            }
            
            // Convert to JSON
            string jsonMessage = JsonUtility.ToJson(commandMsg);
            
            // Send command
            NetworkStream stream = socketConnection.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
            stream.Write(data, 0, data.Length);
            
            Debug.Log($"Sent {command} command to Python backend");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending command: {e.Message}");
        }
    }
    
    /// <summary>
    /// Data class for Python response
    /// </summary>
    [Serializable]
    private class PythonResponse
    {
        public string status;
        public float moveX;
        public float moveZ;
        public string mode;
        public string message;
        public bool paused;
    }
    
    /// <summary>
    /// Data class for updates sent to Python
    /// </summary>
    [Serializable]
    private class PythonUpdate
    {
        public float laserX;
        public float laserY;
        public float laserZ;
        public float controlX;
        public float controlZ;
        public float timestamp;
        public float[] bounds;
        public float heartbeat;
        public string command;  // Optional command field for controlling the connection
        public float catX;      // Cat's X position
        public float catZ;      // Cat's Z position
        public float catY;      // Cat's Y position
        public float catVelX;   // Cat's X velocity
        public float catVelZ;   // Cat's Z velocity
    }

    /// <summary>
    /// Move the laser based on keyboard input
    /// </summary>
    private void MoveWithInput()
    {
        // Get input (can be customized as needed)
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Calculate movement vector
        Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput).normalized;
        
        // Apply movement
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Move the laser automatically according to the selected pattern
    /// </summary>
    private void MoveAutomatically()
    {
        autoMoveTimer += Time.deltaTime * autoMoveSpeed;

        switch (pattern)
        {
            case MovementPattern.CircularPattern:
                ApplyCircularPattern();
                break;
            case MovementPattern.FigureEightPattern:
                ApplyFigureEightPattern();
                break;
            case MovementPattern.RandomPattern:
                ApplyRandomPattern();
                break;
            case MovementPattern.SineWavePattern:
                ApplySineWavePattern();
                break;
        }
    }

    /// <summary>
    /// Apply a circular movement pattern
    /// </summary>
    private void ApplyCircularPattern()
    {
        float radius = Mathf.Min(
            (playAreaBounds.y - playAreaBounds.x) / 2.5f,
            (playAreaBounds.w - playAreaBounds.z) / 2.5f
        );

        float centerX = (playAreaBounds.x + playAreaBounds.y) / 2;
        float centerZ = (playAreaBounds.z + playAreaBounds.w) / 2;

        float x = centerX + radius * Mathf.Cos(autoMoveTimer);
        float z = centerZ + radius * Mathf.Sin(autoMoveTimer);

        transform.position = new Vector3(x, transform.position.y, z);
    }

    /// <summary>
    /// Apply a figure-eight movement pattern
    /// </summary>
    private void ApplyFigureEightPattern()
    {
        float radius = Mathf.Min(
            (playAreaBounds.y - playAreaBounds.x) / 3f,
            (playAreaBounds.w - playAreaBounds.z) / 5f
        );

        float centerX = (playAreaBounds.x + playAreaBounds.y) / 2;
        float centerZ = (playAreaBounds.z + playAreaBounds.w) / 2;

        float x = centerX + radius * Mathf.Sin(autoMoveTimer);
        float z = centerZ + radius * Mathf.Sin(autoMoveTimer * 2);

        transform.position = new Vector3(x, transform.position.y, z);
    }

    /// <summary>
    /// Apply a random movement pattern
    /// </summary>
    private void ApplyRandomPattern()
    {
        // Check if we need a new target
        randomTargetTimer -= Time.deltaTime;
        if (randomTargetTimer <= 0)
        {
            SetNewRandomTarget();
        }

        // Move towards the target
        transform.position = Vector3.MoveTowards(
            transform.position,
            randomTargetPos,
            autoMoveSpeed * Time.deltaTime
        );

        // If we're close to the target, get a new one
        if (Vector3.Distance(transform.position, randomTargetPos) < 0.1f)
        {
            SetNewRandomTarget();
        }
    }

    /// <summary>
    /// Send a ping message to keep the connection alive
    /// </summary>
    private void SendPingIfNeeded()
    {
        // Only send pings at the specified interval
        if (Time.time - lastPingTime < pingInterval)
            return;
            
        lastPingTime = Time.time;
        
        // Only send if connected
        if (socketConnection == null || !socketConnection.Connected)
            return;
            
        try
        {
            // Create a simple ping message
            PythonUpdate pingMsg = new PythonUpdate
            {
                laserX = transform.position.x,
                laserZ = transform.position.z,
                laserY = transform.position.y,
                timestamp = Time.time,
                heartbeat = Time.time,
                command = "ping"
            };
            
            // Add cat position data to ping message if available
            if (catObject != null)
            {
                Vector3 catPosition = catObject.transform.position;
                pingMsg.catX = catPosition.x;
                pingMsg.catY = catPosition.y;
                pingMsg.catZ = catPosition.z;
                
                // Try to get velocity data
                Rigidbody catRigidbody = catObject.GetComponent<Rigidbody>();
                if (catRigidbody != null)
                {
                    Vector3 velocity = catRigidbody.linearVelocity;
                    pingMsg.catVelX = velocity.x;
                    pingMsg.catVelZ = velocity.z;
                }
            }
            
            // Convert to JSON
            string jsonMessage = JsonUtility.ToJson(pingMsg);
            
            // Send ping message
            NetworkStream stream = socketConnection.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
            stream.Write(data, 0, data.Length);
        }
        catch (Exception e)
        {
            // Don't log every ping failure to avoid spam
            // Only set connection to false to trigger reconnect on next update
            isConnected = false;
        }
    }
    
    /// <summary>
    /// Apply a sine wave movement pattern
    /// </summary>
    private void ApplySineWavePattern()
    {
        float width = (playAreaBounds.y - playAreaBounds.x) * 0.8f;
        float height = (playAreaBounds.w - playAreaBounds.z) * 0.8f;

        float centerX = (playAreaBounds.x + playAreaBounds.y) / 2;
        float centerZ = (playAreaBounds.z + playAreaBounds.w) / 2;

        float progress = Mathf.Repeat(autoMoveTimer * 0.5f, 1f);
        float x = centerX + (progress * 2 - 1) * width / 2;
        float z = centerZ + Mathf.Sin(progress * Mathf.PI * 4) * height / 3;

        transform.position = new Vector3(x, transform.position.y, z);
    }

    /// <summary>
    /// Set a new random target position
    /// </summary>
    private void SetNewRandomTarget()
    {
        // Get a random position within the play area
        float x = UnityEngine.Random.Range(playAreaBounds.x, playAreaBounds.y);
        float z = UnityEngine.Random.Range(playAreaBounds.z, playAreaBounds.w);
        
        randomTargetPos = new Vector3(x, transform.position.y, z);
        
        // Set a random time before picking the next target
        randomTargetTimer = UnityEngine.Random.Range(1f, 3f);
    }

    /// <summary>
    /// Constrain the laser pointer to the play area
    /// </summary>
    private void ConstrainToPlayArea()
    {
        Vector3 pos = transform.position;
        
        pos.x = Mathf.Clamp(pos.x, playAreaBounds.x, playAreaBounds.y);
        pos.z = Mathf.Clamp(pos.z, playAreaBounds.z, playAreaBounds.w);
        
        transform.position = pos;
    }

    /// <summary>
    /// Reset the laser pointer to its initial position
    /// </summary>
    public void ResetPosition()
    {
        transform.position = initialPosition;
        autoMoveTimer = 0f;
    }

    /// <summary>
    /// Draw the play area boundaries in the editor
    /// </summary>
    private void OnDrawGizmos()
    {
        // Draw the play area boundaries
        Gizmos.color = Color.green;
        Vector3 center = new Vector3(
            (playAreaBounds.x + playAreaBounds.y) / 2,
            transform.position.y,
            (playAreaBounds.z + playAreaBounds.w) / 2
        );
        
        Vector3 size = new Vector3(
            playAreaBounds.y - playAreaBounds.x,
            0.01f,
            playAreaBounds.w - playAreaBounds.z
        );
        
        Gizmos.DrawWireCube(center, size);
    }
}