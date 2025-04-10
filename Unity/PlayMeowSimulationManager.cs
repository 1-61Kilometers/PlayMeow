using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

/// <summary>
/// PlayMeow Simulation Manager
/// 
/// This script manages a PlayMeow simulation, providing UI controls and integration
/// with the PlayMeowDataRecorder component.
/// </summary>
public class PlayMeowSimulationManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the PlayMeowDataRecorder component")]
    public PlayMeowDataRecorder dataRecorder;

    [Tooltip("Reference to the laser controller")]
    public PlayMeowLaserController laserController;

    [Tooltip("Reference to the cat behavior")]
    public PlayMeowCatBehavior catBehavior;

    [Header("UI Elements")]
    [Tooltip("Button to start/stop recording")]
    public Button recordButton;

    [Tooltip("Text component for record button")]
    public Text recordButtonText;

    [Tooltip("Button to save the recorded data")]
    public Button saveButton;

    [Tooltip("Text component showing recording status")]
    public Text statusText;

    [Tooltip("Toggle for auto-movement")]
    public Toggle autoMoveToggle;

    [Tooltip("Dropdown for selecting movement pattern")]
    public Dropdown patternDropdown;

    [Header("Recording Settings")]
    [Tooltip("Duration of the simulation in seconds (0 for unlimited)")]
    public float simulationDuration = 0f;

    // Internal variables
    private bool isRecording = false;
    private float recordingTimer = 0f;
    private string lastSavedPath = "";

    void Start()
    {
        // Validate references
        if (dataRecorder == null)
        {
            Debug.LogError("PlayMeow Simulation Manager: DataRecorder reference not set!");
            enabled = false;
            return;
        }

        // Setup UI
        SetupUI();
    }

    void Update()
    {
        if (isRecording)
        {
            // Update timer if simulation has a time limit
            if (simulationDuration > 0)
            {
                recordingTimer += Time.deltaTime;
                UpdateStatusText();

                // Stop recording when time is up
                if (recordingTimer >= simulationDuration)
                {
                    StopRecording();
                }
            }
        }
    }

    /// <summary>
    /// Setup UI components and listeners
    /// </summary>
    private void SetupUI()
    {
        // Configure record button
        if (recordButton != null)
        {
            recordButton.onClick.AddListener(ToggleRecording);
            UpdateRecordButtonText();
        }

        // Configure save button
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveRecording);
            saveButton.interactable = false;
        }

        // Configure auto movement toggle
        if (autoMoveToggle != null && laserController != null)
        {
            autoMoveToggle.isOn = laserController.enableAutoMovement;
            autoMoveToggle.onValueChanged.AddListener(ToggleAutoMovement);
        }

        // Configure pattern dropdown
        if (patternDropdown != null && laserController != null)
        {
            patternDropdown.ClearOptions();
            
            // Add pattern options
            patternDropdown.AddOptions(new System.Collections.Generic.List<string> {
                "Circular Pattern",
                "Figure Eight Pattern",
                "Random Pattern",
                "Sine Wave Pattern"
            });
            
            patternDropdown.value = (int)laserController.pattern;
            patternDropdown.onValueChanged.AddListener(ChangeMovementPattern);
            patternDropdown.interactable = laserController.enableAutoMovement;
        }

        UpdateStatusText();
    }

    /// <summary>
    /// Toggle recording state
    /// </summary>
    public void ToggleRecording()
    {
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    /// <summary>
    /// Start recording data
    /// </summary>
    public void StartRecording()
    {
        isRecording = true;
        recordingTimer = 0f;
        
        dataRecorder.StartRecording();
        
        if (saveButton != null)
        {
            saveButton.interactable = false;
        }
        
        UpdateRecordButtonText();
        UpdateStatusText();
    }

    /// <summary>
    /// Stop recording data
    /// </summary>
    public void StopRecording()
    {
        isRecording = false;
        dataRecorder.StopRecordingAndSave();
        lastSavedPath = dataRecorder.GetSaveFilePath();
        
        if (saveButton != null)
        {
            saveButton.interactable = true;
        }
        
        UpdateRecordButtonText();
        UpdateStatusText();
    }

    /// <summary>
    /// Save recording data to CSV
    /// </summary>
    public void SaveRecording()
    {
        // If we're recording, stop first
        if (isRecording)
        {
            StopRecording();
        }
        
        // Get the save path
        string filePath = dataRecorder.GetSaveFilePath();
        
        // If the file exists, show a message about its location
        if (File.Exists(filePath))
        {
            Debug.Log("PlayMeow data saved to: " + filePath);
            if (statusText != null)
            {
                statusText.text = "Data saved to:\n" + filePath;
            }
        }
        else
        {
            Debug.LogWarning("PlayMeow data file not found at: " + filePath);
            if (statusText != null)
            {
                statusText.text = "Error: Data file not found";
            }
        }
    }

    /// <summary>
    /// Toggle automatic movement of the laser pointer
    /// </summary>
    public void ToggleAutoMovement(bool isOn)
    {
        if (laserController != null)
        {
            laserController.enableAutoMovement = isOn;
            
            if (patternDropdown != null)
            {
                patternDropdown.interactable = isOn;
            }
        }
    }

    /// <summary>
    /// Change the movement pattern of the laser pointer
    /// </summary>
    public void ChangeMovementPattern(int patternIndex)
    {
        if (laserController != null)
        {
            laserController.pattern = (PlayMeowLaserController.MovementPattern)patternIndex;
        }
    }

    /// <summary>
    /// Update the record button text based on current state
    /// </summary>
    private void UpdateRecordButtonText()
    {
        if (recordButtonText != null)
        {
            recordButtonText.text = isRecording ? "Stop Recording" : "Start Recording";
        }
    }

    /// <summary>
    /// Update the status text with current recording information
    /// </summary>
    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            if (isRecording)
            {
                if (simulationDuration > 0)
                {
                    float timeRemaining = Mathf.Max(0, simulationDuration - recordingTimer);
                    statusText.text = string.Format("Recording: {0:0.0} / {1:0.0} sec", 
                        recordingTimer, simulationDuration);
                }
                else
                {
                    statusText.text = string.Format("Recording: {0:0.0} sec", recordingTimer);
                }
            }
            else if (!string.IsNullOrEmpty(lastSavedPath))
            {
                statusText.text = "Ready. Last saved to:\n" + Path.GetFileName(lastSavedPath);
            }
            else
            {
                statusText.text = "Ready to record.";
            }
        }
    }
}