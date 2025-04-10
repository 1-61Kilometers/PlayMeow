using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

/// <summary>
/// PlayMeow Data Recorder
/// 
/// This Unity script records simulation data for the PlayMeow project.
/// It tracks a cat GameObject and a laser pointer GameObject, recording their positions
/// and other relevant data to generate a compatible CSV file.
/// </summary>
public class PlayMeowDataRecorder : MonoBehaviour
{
    [Header("Game Objects")]
    [Tooltip("Reference to the cat GameObject")]
    public GameObject catObject;

    [Tooltip("Reference to the laser pointer GameObject")]
    public GameObject laserObject;

    [Header("Recording Settings")]
    [Tooltip("Path to save the CSV file (relative to Application.persistentDataPath)")]
    public string saveFilePath = "playmeow_data.csv";

    [Tooltip("Recording frequency in seconds")]
    public float recordingInterval = 0.1f;

    [Tooltip("Automatically start recording on game start")]
    public bool autoStartRecording = false;

    [Header("Simulation Parameters")]
    [Tooltip("Boundaries of the play area [minX, maxX, minZ, maxZ]")]
    public Vector4 playAreaBounds = new Vector4(-2f, 2f, -2f, 2f);

    [Header("Debug")]
    [Tooltip("Display debug information in the console")]
    public bool debugMode = true;

    // Internal variables
    private List<PlayMeowDataRow> dataBuffer = new List<PlayMeowDataRow>();
    private float recordingTimer = 0f;
    private bool isRecording = false;
    private float simulationStartTime;
    private int engagementIndicator = 0;
    private float timeSinceEngagement = 0f;

    // Structure to hold data for each row
    private struct PlayMeowDataRow
    {
        public float timestamp;
        public float cat_x;
        public float cat_y;
        public float cat_z;
        public float laser_x;
        public float laser_y;
        public float laser_z;
        public int engagement_indicator;
        public float time_since_engagement;
        public float distance_to_boundary_x;
        public float distance_to_boundary_z;

        public PlayMeowDataRow(float timestamp, Vector3 catPos, Vector3 laserPos, int engagement, 
                             float timeSinceEngagement, float distToBoundaryX, float distToBoundaryZ)
        {
            this.timestamp = timestamp;
            this.cat_x = catPos.x;
            this.cat_y = catPos.y;
            this.cat_z = catPos.z;
            this.laser_x = laserPos.x;
            this.laser_y = laserPos.y;
            this.laser_z = laserPos.z;
            this.engagement_indicator = engagement;
            this.time_since_engagement = timeSinceEngagement;
            this.distance_to_boundary_x = distToBoundaryX;
            this.distance_to_boundary_z = distToBoundaryZ;
        }
    }

    void Start()
    {
        // Validate required references
        if (catObject == null || laserObject == null)
        {
            Debug.LogError("PlayMeow Data Recorder: Cat or laser GameObject references not set!");
            enabled = false;
            return;
        }

        simulationStartTime = Time.time;

        if (autoStartRecording)
        {
            StartRecording();
        }
    }

    void Update()
    {
        if (!isRecording)
            return;

        // Update timer
        recordingTimer += Time.deltaTime;

        // Record data at the specified interval
        if (recordingTimer >= recordingInterval)
        {
            RecordDataFrame();
            recordingTimer = 0f;
        }
    }

    /// <summary>
    /// Start recording data
    /// </summary>
    public void StartRecording()
    {
        isRecording = true;
        simulationStartTime = Time.time;
        if (debugMode)
            Debug.Log("PlayMeow Data Recorder: Recording started");
    }

    /// <summary>
    /// Stop recording and save data to CSV file
    /// </summary>
    public void StopRecordingAndSave()
    {
        isRecording = false;
        SaveToCSV();
        if (debugMode)
            Debug.Log("PlayMeow Data Recorder: Recording stopped and data saved");
    }

    /// <summary>
    /// Record a single data frame
    /// </summary>
    private void RecordDataFrame()
    {
        if (catObject == null || laserObject == null)
            return;

        // Calculate simulation time
        float currentTime = Time.time - simulationStartTime;

        // Get positions
        Vector3 catPosition = catObject.transform.position;
        Vector3 laserPosition = laserObject.transform.position;

        // Calculate distance to boundaries
        float distToBoundaryX = Mathf.Min(
            Mathf.Abs(laserPosition.x - playAreaBounds.x),
            Mathf.Abs(laserPosition.x - playAreaBounds.y)
        );

        float distToBoundaryZ = Mathf.Min(
            Mathf.Abs(laserPosition.z - playAreaBounds.z),
            Mathf.Abs(laserPosition.z - playAreaBounds.w)
        );

        // Create and add data row
        PlayMeowDataRow dataRow = new PlayMeowDataRow(
            currentTime,
            catPosition,
            laserPosition,
            engagementIndicator,
            timeSinceEngagement,
            distToBoundaryX,
            distToBoundaryZ
        );

        dataBuffer.Add(dataRow);

        // Update time since engagement
        if (engagementIndicator == 1)
        {
            timeSinceEngagement = 0;
        }
        else
        {
            timeSinceEngagement += recordingInterval;
        }
    }

    /// <summary>
    /// Set the cat engagement indicator (call this from your cat behavior script)
    /// </summary>
    /// <param name="isEngaged">Whether the cat is currently engaged with the laser</param>
    public void SetEngagement(bool isEngaged)
    {
        engagementIndicator = isEngaged ? 1 : 0;
        
        if (isEngaged)
            timeSinceEngagement = 0f;
    }

    /// <summary>
    /// Save recorded data to CSV file
    /// </summary>
    private void SaveToCSV()
    {
        if (dataBuffer.Count == 0)
        {
            Debug.LogWarning("PlayMeow Data Recorder: No data to save");
            return;
        }

        string filePath = Path.Combine(Application.persistentDataPath, saveFilePath);
        
        try
        {
            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            // Build CSV content
            StringBuilder csv = new StringBuilder();
            
            // Add header
            csv.AppendLine("timestamp,cat_x,cat_y,cat_z,laser_x,laser_y,laser_z,engagement_indicator,time_since_engagement,distance_to_boundary_x,distance_to_boundary_z");

            // Add data rows
            foreach (var row in dataBuffer)
            {
                csv.AppendLine(string.Format(
                    CultureInfo.InvariantCulture, // Use invariant culture to ensure decimal points
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    row.timestamp,
                    row.cat_x,
                    row.cat_y,
                    row.cat_z,
                    row.laser_x,
                    row.laser_y,
                    row.laser_z,
                    row.engagement_indicator,
                    row.time_since_engagement,
                    row.distance_to_boundary_x,
                    row.distance_to_boundary_z
                ));
            }

            // Write to file
            File.WriteAllText(filePath, csv.ToString());
            
            if (debugMode)
                Debug.Log("PlayMeow Data Recorder: Saved " + dataBuffer.Count + " records to " + filePath);
            
            // Clear buffer after saving
            dataBuffer.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError("PlayMeow Data Recorder: Error saving data - " + e.Message);
        }
    }

    /// <summary>
    /// Clear the current data buffer without saving
    /// </summary>
    public void ClearDataBuffer()
    {
        int count = dataBuffer.Count;
        dataBuffer.Clear();
        
        if (debugMode)
            Debug.Log("PlayMeow Data Recorder: Cleared " + count + " records from buffer");
    }

    /// <summary>
    /// Get the full path where the CSV will be saved
    /// </summary>
    public string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFilePath);
    }
}