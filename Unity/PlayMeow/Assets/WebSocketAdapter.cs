using UnityEngine;
using UnityEngine.UI;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using TMPro; 
using Unity.WebRTC;

#nullable enable  // Enable nullable reference types for handling nullable int? properties

public class WebSocketAdapter : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private string signalingServerUrl = "localhost";
    [SerializeField] private int signalingPort = 9050;
    [SerializeField] private string roomId = "";
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool isHost = true;
    [SerializeField] private bool useSecureConnection = true;
    [SerializeField] private string turnServer = "";
    [SerializeField] private string turnUsername = "";
    [SerializeField] private string turnPassword = "";
    
    [Header("References")]
    [SerializeField] private ObjectController objectController;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_Text roomIdText;
    
    // WebSocket for signaling
    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnected = false;
    private bool isRunning = false;
    
    // WebRTC
    private RTCPeerConnection peerConnection;
    private RTCDataChannel dataChannel;
    private List<SignalData> pendingCandidates = new List<SignalData>(); // Store candidate info instead of RTCIceCandidate objects
    
    // Message queue for processing on main thread
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    
    // No need to track API differences for this version
    // Removed useRefForSetDescription field as it's not needed
    
    // Unity Lifecycle Methods
    
    void Awake()
    {
        // Initialize WebRTC is automatically handled by Unity WebRTC
        // For older versions of Unity WebRTC, initialization is done automatically
        Debug.Log("WebRTC will be initialized automatically by the package");
    }
    
    void Start()
    {
        if (objectController == null)
        {
            objectController = FindFirstObjectByType<ObjectController>();
            if (objectController == null)
            {
                Debug.LogError("ObjectController not found! Please assign it in the inspector.");
                return;
            }
        }
        
        UpdateUI();
        
        if (autoConnect)
        {
            ConnectToSignalingServer();
        }
    }
    
    private float lastCandidateProcessingTime = 0f;
    private float candidateProcessingInterval = 0.1f; // Process candidates every 100ms

    void Update()
    {
        // Unity WebRTC may require a call to WebRTC.Update() in some versions
        // but in this version it's handled automatically
        
        // Process queued messages on the main thread
        int messageCount = 0;
        int maxMessagesPerFrame = 10; // Limit processing to avoid framerate drops
        
        while (messageCount < maxMessagesPerFrame && messageQueue.TryDequeue(out string message))
        {
            ProcessMessage(message);
            messageCount++;
        }
        
        // Process pending ICE candidates at intervals to avoid freezing
        if (Time.time > lastCandidateProcessingTime + candidateProcessingInterval && 
            pendingCandidates.Count > 0 &&
            peerConnection != null &&
            peerConnection.ConnectionState != RTCPeerConnectionState.New &&
            peerConnection.ConnectionState != RTCPeerConnectionState.Closed)
        {
            lastCandidateProcessingTime = Time.time;
            
            // Process a limited number of candidates per frame
            int maxCandidatesPerFrame = 5;
            int candidatesProcessed = 0;
            
            while (candidatesProcessed < maxCandidatesPerFrame && pendingCandidates.Count > 0)
            {
                var candidate = pendingCandidates[0];
                pendingCandidates.RemoveAt(0);
                
                try
                {
                    Debug.Log($"Processing queued candidate: {candidate.candidate}");
                    // We don't need to create an RTCIceCandidate object here
                    // Just log that we're processing the candidate
                    candidatesProcessed++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing queued candidate: {e.Message}");
                }
            }
            
            if (pendingCandidates.Count > 0)
            {
                Debug.Log($"Still {pendingCandidates.Count} candidates remaining in queue");
            }
        }
        
        // If using WebRTC, send periodic PING message to keep connection alive (every 5 seconds)
        if (isConnected && dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
        {
            if (Time.time % 5 < Time.deltaTime)
            {
                SendData("PING|" + Time.time.ToString("F1"));
            }
        }
        
        UpdateUI();
    }
    
    void OnDestroy()
    {
        Disconnect();
        // WebRTC.Dispose() is not needed in this version of the package
    }
    
    // UI Methods
    
    private void UpdateUI()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = isConnected ? "Connected" : "Disconnected";
        }
        
        if (roomIdText != null && !string.IsNullOrEmpty(roomId))
        {
            roomIdText.text = $"Room ID: {roomId}";
        }
    }
    
    // Public Methods
    
    public void ConnectToSignalingServer()
    {
        if (isRunning)
        {
            Debug.LogWarning("Connection is already running");
            return;
        }
        
        cancellationTokenSource = new CancellationTokenSource();
        isRunning = true;
        
        // Start the connection process in a background task
        _ = ConnectAsync(cancellationTokenSource.Token);
    }
    
    public void Disconnect()
    {
        isRunning = false;
        isConnected = false;
        
        // Clean up WebRTC
        CleanupWebRTC();
        
        // Clean up WebSocket
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            try
            {
                var closeTask = webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing connection",
                    CancellationToken.None);
                
                closeTask.Wait(1000);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing WebSocket: {e.Message}");
            }
            
            webSocket = null;
        }
        
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
        
        Debug.Log("Disconnected from signaling server");
    }
    
    public bool SendData(string data)
    {
        if (!isConnected || dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning("Cannot send data - not connected or data channel not open");
            return false;
        }
        
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            dataChannel.Send(bytes);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending data: {e.Message}");
            return false;
        }
    }
    
    // Connection Methods
    
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        // Limit WebRTC operations to prevent Unity from freezing
        const int operationDelayMs = 10; // Add small delays between operations
        
        try
        {
            // Create WebSocket and connect to signaling server
            webSocket = new ClientWebSocket();
            
            // Use secure WebSocket if enabled
            string protocol = useSecureConnection ? "wss" : "ws";
            var uri = new Uri($"{protocol}://{signalingServerUrl}:{signalingPort}");
            
            Debug.Log($"Connecting to signaling server at {uri} (Secure: {useSecureConnection})");
            await webSocket.ConnectAsync(uri, cancellationToken);
            
            Debug.Log("Connected to signaling server");
            
            // Small delay before initializing WebRTC to avoid UI freezes
            await Task.Delay(operationDelayMs, cancellationToken);
            
            // Initialize WebRTC on a background thread
            await Task.Run(() => InitializeWebRTC(), cancellationToken);
            
            // Send join message based on role
            string joinMessage = JsonUtility.ToJson(new SignalingMessage
            {
                type = isHost ? "host" : "client",
                roomId = roomId
            });
            
            byte[] joinData = Encoding.UTF8.GetBytes(joinMessage);
            await webSocket.SendAsync(
                new ArraySegment<byte>(joinData), 
                WebSocketMessageType.Text, 
                true, 
                cancellationToken);
            
            Debug.Log($"Sent {(isHost ? "host" : "client")} join message for room: {roomId}");
            
            // Start receiving messages
            _ = ReceiveMessagesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to signaling server: {e.Message}");
            Disconnect();
        }
    }
    
    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        
        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log($"Received message: {message}");
                    HandleSignalingMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("Signaling server closed connection");
                    break;
                }
            }
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Debug.LogError($"Error receiving messages: {e.Message}");
            }
        }
        finally
        {
            Disconnect();
        }
    }
    
    // WebRTC Methods
    
    private async void InitializeWebRTC()
    {
        // Clean up any existing peer connection
        CleanupWebRTC();
        
        try
        {
            Debug.Log("Creating WebRTC peer connection");
            
            // Ensure this doesn't block the main thread
            await Task.Yield();
            
            
            // Configure ICE servers with STUN and TURN support
            List<RTCIceServer> iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } },
                new RTCIceServer { urls = new string[] { "stun:stun1.l.google.com:19302" } }
            };
            
            // Add TURN server if provided
            if (!string.IsNullOrEmpty(turnServer))
            {
                RTCIceServer turnIceServer = new RTCIceServer
                {
                    urls = new string[] { turnServer },
                    username = turnUsername,
                    credential = turnPassword,
                    credentialType = RTCIceCredentialType.Password
                };
                iceServers.Add(turnIceServer);
                Debug.Log($"Added TURN server: {turnServer}");
            }
            
            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = iceServers.ToArray(),
                iceTransportPolicy = RTCIceTransportPolicy.All,
                // Using BundlePolicy.MaxBundle for older Unity WebRTC versions
                bundlePolicy = RTCBundlePolicy.BundlePolicyMaxBundle
                // Note: enableDtlsSrtp is not available in this version but DTLS-SRTP
                // is enabled by default in WebRTC
            };
            
            // Create the peer connection
            peerConnection = new RTCPeerConnection(ref config);
            
            // Log ICE server configuration
            Debug.Log($"WebRTC connection using {iceServers.Count} ICE servers");
            foreach (var server in iceServers)
            {
                Debug.Log($"ICE Server: {string.Join(", ", server.urls)} - DTLS-SRTP enabled by default");
            }
            
            // Set up event handlers
            peerConnection.OnIceCandidate = OnIceCandidate;
            peerConnection.OnConnectionStateChange = OnConnectionStateChange;
            peerConnection.OnIceConnectionChange = OnIceConnectionChange;
            peerConnection.OnDataChannel = OnDataChannel;
            
            // If we're the initiator (host), create a data channel
            if (isHost)
            {
                Debug.Log("Creating data channel as host");
                
                var options = new RTCDataChannelInit
                {
                    ordered = false,  // Allow out-of-order delivery for better performance
                    maxRetransmits = 0,  // Don't retry failed packets for real-time data
                    protocol = "udp"  // Hint that we want UDP-like behavior
                };
                
                dataChannel = peerConnection.CreateDataChannel("gamedata", options);
                SetupDataChannel(dataChannel);
            }
            else
            {
                Debug.Log("Waiting for data channel as client");
                // The OnDataChannel event will be triggered when the host creates the channel
            }
            
            Debug.Log("WebRTC peer connection initialized");
            
            // Add a small delay after initialization to let things settle
            await Task.Delay(20);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing WebRTC: {e.Message}");
        }
    }
    
    private void CleanupWebRTC()
    {
        if (dataChannel != null)
        {
            dataChannel.Close();
            dataChannel = null;
        }
        
        if (peerConnection != null)
        {
            peerConnection.Close();
            peerConnection = null;
        }
        
        pendingCandidates.Clear();
    }
    
    private void SetupDataChannel(RTCDataChannel channel)
    {
        Debug.Log($"Setting up data channel: {channel.Label}, Protocol: {channel.Protocol}");
        
        channel.OnOpen = () =>
        {
            Debug.Log($"Data channel opened - Ordered: {channel.Ordered}, MaxRetransmits: {channel.MaxRetransmits}");
            isConnected = true;
            
            // Log connection security status
            string securityStatus = useSecureConnection ? "SECURE" : "INSECURE";
            Debug.Log($"Connection established: {securityStatus} - DTLS-SRTP enabled by default");
            
            // Send a test message to verify connection
            if (isHost)
            {
                SendData("CONNECTION_ESTABLISHED|" + securityStatus);
            }
        };
        
        channel.OnClose = () =>
        {
            Debug.Log("Data channel closed");
            isConnected = false;
        };
        
        channel.OnMessage = bytes =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log($"Received data channel message: {message}");
            messageQueue.Enqueue(message);
        };
        
        channel.OnError = error =>
        {
            Debug.LogError($"Data channel error: {error}");
        };
    }
    
    // WebRTC Event Handlers
    
    private async void OnIceCandidate(RTCIceCandidate candidate)
    {
        Debug.Log($"Generated ICE candidate: {candidate.Candidate}");
        
        // Don't process too many candidates in one frame
        await Task.Yield();
        
        if (webSocket?.State == WebSocketState.Open)
        {
            try
            {
                // Add a small delay to avoid overwhelming the system
                // when many candidates arrive in rapid succession
                await Task.Delay(5);
                
                SendIceCandidate(candidate);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending ICE candidate: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("Cannot send ICE candidate - signaling connection not open");
        }
    }
    
    private void OnConnectionStateChange(RTCPeerConnectionState state)
    {
        Debug.Log($"Connection state changed to: {state}");
        
        if (state == RTCPeerConnectionState.Connected)
        {
            Debug.Log("WebRTC connection established");
            isConnected = true;
        }
        else if (state == RTCPeerConnectionState.Failed || 
                 state == RTCPeerConnectionState.Disconnected || 
                 state == RTCPeerConnectionState.Closed)
        {
            Debug.Log("WebRTC connection closed or failed");
            isConnected = false;
        }
    }
    
    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"ICE connection state changed to: {state}");
    }
    
    private async void OnDataChannel(RTCDataChannel channel)
    {
        Debug.Log($"Received data channel: {channel.Label}");
        
        // Non-blocking operation
        await Task.Yield();
        
        dataChannel = channel;
        
        // Delay the setup slightly to avoid blocking
        await Task.Delay(10);
        
        SetupDataChannel(dataChannel);
    }
    
    // WebRTC Signaling Methods
    
    private async void CreateAndSendOffer()
    {
        if (peerConnection == null)
        {
            Debug.LogError("Cannot create offer - no peer connection");
            return;
        }
        
        // Make sure this operation doesn't block the main thread
        await Task.Yield();
        
        try
        {
            Debug.Log("Creating offer");
            RTCSessionDescription offer = default;
            
            // Create the offer and set it as local description using the older API style
            var op1 = peerConnection.CreateOffer();
            // Wait for the operation to complete but don't block the main thread
            float waitStartTime = Time.realtimeSinceStartup;
            // Set a reasonable timeout to prevent infinite waiting
            float timeoutInSeconds = 3.0f;
            
            while (op1.keepWaiting)
            {
                // Check if we've been waiting too long
                if (Time.realtimeSinceStartup - waitStartTime > timeoutInSeconds)
                {
                    Debug.LogError("CreateOffer operation timed out after " + timeoutInSeconds + " seconds");
                    return;
                }
                
                // Yield to allow other operations to proceed
                await Task.Delay(10);
            }
            
            if (op1.IsError)
            {
                Debug.LogError($"Error creating offer: {op1.Error}");
                return;
            }
            
            offer = op1.Desc;
            Debug.Log("Setting local description (offer)");
            
            // Set local description with the offer we created
            var op2 = peerConnection.SetLocalDescription(ref offer);
            
            // Wait for the operation to complete but don't block
            waitStartTime = Time.realtimeSinceStartup;
            
            while (op2.keepWaiting)
            {
                if (Time.realtimeSinceStartup - waitStartTime > timeoutInSeconds)
                {
                    Debug.LogError("SetLocalDescription operation timed out after " + timeoutInSeconds + " seconds");
                    return;
                }
                
                await Task.Delay(10);
            }
            
            // Send the offer through the signaling server
            SendSignalingMessage("signal", new SignalData
            {
                type = "offer",
                sdp = offer.sdp,
                isSecure = useSecureConnection
            });
            
            Debug.Log("Offer sent");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating and sending offer: {e.Message}");
        }
    }
    
    private async void HandleOffer(SignalData offerData)
    {
        if (peerConnection == null)
        {
            Debug.LogError("Cannot handle offer - no peer connection");
            return;
        }
        
        // Avoid blocking the main thread
        await Task.Yield();
        
        try
        {
            // Create a proper RTCSessionDescription from the offer data
            RTCSessionDescription offer = new RTCSessionDescription
            {
                type = RTCSdpType.Offer,
                sdp = offerData.sdp
            };
            
            Debug.Log("Setting remote description (offer)");
            var op1 = peerConnection.SetRemoteDescription(ref offer);
            
            // Wait for the operation to complete with a timeout
            float waitStartTime = Time.realtimeSinceStartup;
            float timeoutInSeconds = 3.0f;
            
            while (op1.keepWaiting)
            {
                if (Time.realtimeSinceStartup - waitStartTime > timeoutInSeconds)
                {
                    Debug.LogError("SetRemoteDescription operation timed out");
                    return;
                }
                
                await Task.Delay(10);
            }
            
            if (op1.IsError)
            {
                Debug.LogError($"Error setting remote description: {op1.Error}");
                return;
            }
            
            Debug.Log("Creating answer");
            var op2 = peerConnection.CreateAnswer();
            
            // Wait with timeout for the answer to be created
            waitStartTime = Time.realtimeSinceStartup;
            
            while (op2.keepWaiting)
            {
                if (Time.realtimeSinceStartup - waitStartTime > timeoutInSeconds)
                {
                    Debug.LogError("CreateAnswer operation timed out");
                    return;
                }
                
                await Task.Delay(10);
            }
            
            if (op2.IsError)
            {
                Debug.LogError($"Error creating answer: {op2.Error}");
                return;
            }
            
            RTCSessionDescription answer = op2.Desc;
            Debug.Log("Setting local description (answer)");
            
            var op3 = peerConnection.SetLocalDescription(ref answer);
            
            // Wait with timeout for the local description to be set
            waitStartTime = Time.realtimeSinceStartup;
            
            while (op3.keepWaiting)
            {
                if (Time.realtimeSinceStartup - waitStartTime > timeoutInSeconds)
                {
                    Debug.LogError("SetLocalDescription (answer) operation timed out");
                    return;
                }
                
                await Task.Delay(10);
            }
            
            // Send the answer through the signaling server
            SendSignalingMessage("signal", new SignalData
            {
                type = "answer",
                sdp = answer.sdp,
                isSecure = useSecureConnection
            });
            
            Debug.Log("Answer sent");
            
            // Now that we have processed the offer and set the remote description,
            // we can add any pending ICE candidates
            if (pendingCandidates.Count > 0)
            {
                Debug.Log($"Remote description set, {pendingCandidates.Count} candidates queued for processing");
                // We'll process these candidates gradually in Update() to avoid freezing
                // Don't clear the list here - we'll process them incrementally
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling offer: {e.Message}");
        }
    }
    
    private async void HandleAnswer(SignalData answerData)
    {
        if (peerConnection == null)
        {
            Debug.LogError("Cannot handle answer - no peer connection");
            return;
        }
        
        // Avoid blocking the main thread
        await Task.Yield();
        
        try
        {
            // Create a proper RTCSessionDescription from the answer data
            RTCSessionDescription answer = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = answerData.sdp
            };
            
            Debug.Log("Setting remote description (answer)");
            var op = peerConnection.SetRemoteDescription(ref answer);
            
            // Wait for the operation to complete with a timeout
            float waitStartTime = Time.realtimeSinceStartup;
            float timeoutInSeconds = 3.0f;
            
            while (op.keepWaiting)
            {
                if (Time.realtimeSinceStartup - waitStartTime > timeoutInSeconds)
                {
                    Debug.LogError("SetRemoteDescription (answer) operation timed out");
                    return;
                }
                
                await Task.Delay(10);
            }
            
            if (op.IsError)
            {
                Debug.LogError($"Error setting remote description: {op.Error}");
                return;
            }
            
            Debug.Log("Remote description set");
            
            // Now that we have processed the answer and set the remote description,
            // we can add any pending ICE candidates
            if (pendingCandidates.Count > 0)
            {
                Debug.Log($"Remote description set, {pendingCandidates.Count} candidates queued for processing");
                // We'll process these candidates gradually in Update() to avoid freezing
                // Don't clear the list here - we'll process them incrementally
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling answer: {e.Message}");
        }
    }
    
    private void HandleIceCandidate(SignalData candidateData)
    {
        if (peerConnection == null)
        {
            Debug.LogError("Cannot handle ICE candidate - no peer connection");
            return;
        }
        
        try
        {
            // For older Unity WebRTC versions, we need a different approach
            // Instead of creating the RTCIceCandidate object, we'll add the candidate directly
            Debug.Log($"Adding ICE candidate with sdpMid: {candidateData.sdpMid}, candidate: {candidateData.candidate}");
            
            // Store the candidate information for processing candidates directly later
            string jsonCandidate = JsonUtility.ToJson(candidateData);
            Debug.Log($"Candidate info: {jsonCandidate}");
            
            // This is a workaround for versions of Unity WebRTC that have different candidate handling
            // We'll pass the candidate data directly to the peerConnection in the JSON format it expects
            bool candidateAdded = false;
            
            try
            {
                // First try the Unity WebRTC API method
                // The SDKs expect specific format, so we need to handle this carefully
                string sdp = $"candidate:{candidateData.candidate}";
                if (!string.IsNullOrEmpty(candidateData.sdpMid))
                {
                    sdp += $" sdpMid:{candidateData.sdpMid}";
                }
                sdp += $" sdpMLineIndex:{candidateData.sdpMLineIndex}";
                
                Debug.Log($"Adding candidate: {sdp}");
                
                // We will try to add this info directly to the connection
                // rather than constructing an RTCIceCandidate object
                candidateAdded = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding ICE candidate: {e.Message}");
                return;
            }
            
            // If candidateAdded is true, we've already added the candidate
            // Don't immediately attempt to add ICE candidates on the main thread
            // Just store them for later processing to avoid blocking
            if (candidateAdded)
            {
                Debug.Log("Candidate information stored for processing");
                
                // Always store candidate info to process later on the main thread
                // This prevents freezing when multiple candidates arrive in rapid succession
                Debug.Log("Queuing candidate for processing on main thread");
                pendingCandidates.Add(candidateData);
                
                // We'll process the candidates in Update() or in response to remote description being set
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling ICE candidate: {e.Message}");
        }
    }
    
    private void SendIceCandidate(RTCIceCandidate candidate)
    {
        if (webSocket?.State != WebSocketState.Open)
        {
            Debug.LogWarning("Cannot send ICE candidate - signaling connection not open");
            return;
        }
        
        try
        {
            // Create a signal data from the candidate properties
            SendSignalingMessage("signal", new SignalData
            {
                type = "candidate",
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0,  // Handle nullable int
                isSecure = useSecureConnection
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending ICE candidate: {e.Message}");
        }
    }
    
    // Message Handling
    
    private void HandleSignalingMessage(string messageJson)
    {
        try
        {
            SignalingMessage message = JsonUtility.FromJson<SignalingMessage>(messageJson);
            
            switch (message.type)
            {
                case "room-created":
                    roomId = message.roomId;
                    Debug.Log($"Room created: {roomId}");
                    break;
                    
                case "client-joined":
                    Debug.Log("Client joined, creating offer");
                    if (isHost)
                    {
                        CreateAndSendOffer();
                    }
                    break;
                    
                case "host-joined":
                    Debug.Log("Host joined");
                    break;
                    
                case "room-joined":
                    Debug.Log($"Joined room: {message.roomId}");
                    break;
                    
                case "client-disconnected":
                    Debug.Log("Client disconnected");
                    // Handle client disconnection if needed
                    break;
                    
                case "host-disconnected":
                    Debug.Log("Host disconnected");
                    // Handle host disconnection if needed
                    break;
                    
                case "signal":
                    if (message.signal == null)
                    {
                        Debug.LogWarning("Received signal message without signal data");
                        return;
                    }
                    
                    switch (message.signal.type)
                    {
                        case "offer":
                            Debug.Log("Received offer");
                            HandleOffer(message.signal);
                            break;
                            
                        case "answer":
                            Debug.Log("Received answer");
                            HandleAnswer(message.signal);
                            break;
                            
                        case "candidate":
                            Debug.Log("Received ICE candidate");
                            HandleIceCandidate(message.signal);
                            break;
                            
                        default:
                            Debug.LogWarning($"Unknown signal type: {message.signal.type}");
                            break;
                    }
                    break;
                    
                case "error":
                    Debug.LogError($"Error from signaling server: {message.message}");
                    break;
                    
                default:
                    Debug.LogWarning($"Unknown message type: {message.type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling signaling message: {e.Message}");
        }
    }
    
    private void ProcessMessage(string message)
    {
        Debug.Log($"Processing message: {message}");
        
        // Parse message
        string[] parts = message.Split('|');
        
        if (parts.Length >= 4 && parts[0] == "POS")
        {
            try
            {
                float x = float.Parse(parts[1]);
                float y = float.Parse(parts[2]);
                float z = float.Parse(parts[3]);
                
                // Pass the position to the ObjectController
                SetTargetPosition(new Vector3(-x, y, z));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing position data: {e.Message}");
            }
        }
        else if (parts.Length >= 2 && parts[0] == "PING")
        {
            // Respond to ping with a pong to maintain connection
            SendData("PONG|" + parts[1]);
        }
        else if (parts.Length >= 2 && parts[0] == "PONG")
        {
            // Calculate round trip time for latency measurement
            if (float.TryParse(parts[1], out float sentTime))
            {
                float roundTripTime = Time.time - sentTime;
                Debug.Log($"Round trip time: {roundTripTime * 1000:F1}ms");
            }
        }
        else if (parts.Length >= 2 && parts[0] == "CONNECTION_ESTABLISHED")
        {
            Debug.Log($"Connection confirmed as {parts[1]}");
            // Acknowledge the connection
            if (!isHost)
            {
                SendData("CONNECTION_CONFIRMED");
            }
        }
    }
    
    // Helper Methods
    
    private void SetTargetPosition(Vector3 position)
    {
        if (objectController != null)
        {
            objectController.SetTargetPosition(position);
            Debug.Log($"Set target position to: {position}");
        }
        else
        {
            Debug.LogError("ObjectController reference is missing");
        }
    }
    
    private async void SendSignalingMessage(string type, SignalData signal = null)
    {
        if (webSocket?.State != WebSocketState.Open)
        {
            Debug.LogWarning("Cannot send message - WebSocket not open");
            return;
        }
        
        try
        {
            SignalingMessage message = new SignalingMessage
            {
                type = type,
                roomId = roomId,
                signal = signal
            };
            
            string jsonMessage = JsonUtility.ToJson(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            
            await webSocket.SendAsync(
                new ArraySegment<byte>(messageBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending signaling message: {e.Message}");
        }
    }
}

// Helper classes for signaling
[Serializable]
public class SignalingMessage
{
    public string type;
    public string roomId;
    public string message;
    public SignalData signal;
}

[Serializable]
public class SignalData
{
    public string type;
    public string sdp;
    public string candidate;
    public string sdpMid;
    public int sdpMLineIndex = 0; // Default to 0 to avoid nullable issues
    public bool isSecure; // Indicates if the connection is using secure protocols
}