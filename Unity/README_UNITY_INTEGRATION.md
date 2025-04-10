# PlayMeow Unity Integration

This guide explains how to use the Unity scripts provided in this repository to create a data collection environment for the PlayMeow project.

## Overview

The Unity integration consists of the following scripts:

1. **PlayMeowDataRecorder.cs** - Records cat and laser pointer positions to generate compatible CSV files
2. **PlayMeowCatBehavior.cs** - Provides simulated cat behavior that follows a laser pointer
3. **PlayMeowLaserController.cs** - Controls the laser pointer movement (manual or automatic patterns)
4. **PlayMeowSimulationManager.cs** - Manages the simulation with UI controls

## Setup Instructions

Follow these steps to set up the Unity environment:

### 1. Create a New Unity Project

1. Open Unity Hub and create a new 3D project
2. Import any required packages (no special requirements)

### 2. Create Game Objects

Create the following game objects in your scene:

1. **Cat Object**: 
   - Create a 3D object to represent the cat (cube, sphere, or custom model)
   - Position it at a suitable starting location on the ground plane

2. **Laser Pointer Object**:
   - Create a small 3D object to represent the laser pointer (small sphere works well)
   - Add a particle effect or light to make it visually distinct

3. **Ground Plane**:
   - Create a 3D plane to represent the floor/play area
   - Scale it to match your desired play area boundaries

4. **Manager Object**:
   - Create an empty game object named "Manager" to hold the simulation scripts

5. **UI Canvas** (optional):
   - Create a UI Canvas for the simulation controls
   - Add buttons, text elements, and other UI components as needed

### 3. Add Scripts to Objects

1. Add the scripts from this repository to your Unity project
2. Assign the scripts to the appropriate game objects:
   - **PlayMeowCatBehavior.cs** → Cat Object
   - **PlayMeowLaserController.cs** → Laser Pointer Object
   - **PlayMeowDataRecorder.cs** → Manager Object
   - **PlayMeowSimulationManager.cs** → Manager Object

### 4. Configure Script References

Set up the references between scripts:

1. In the **PlayMeowCatBehavior** component:
   - Assign the Laser Pointer object to the "Laser Pointer" field
   - Assign the PlayMeowDataRecorder component to the "Data Recorder" field

2. In the **PlayMeowDataRecorder** component:
   - Assign the Cat Object to the "Cat Object" field
   - Assign the Laser Pointer Object to the "Laser Object" field

3. In the **PlayMeowSimulationManager** component:
   - Assign the PlayMeowDataRecorder to the "Data Recorder" field
   - Assign the PlayMeowLaserController to the "Laser Controller" field
   - Assign the PlayMeowCatBehavior to the "Cat Behavior" field
   - Assign UI elements if using the UI Canvas

### 5. Configure Settings

Adjust the settings in each component to suit your simulation needs:

1. **PlayMeowDataRecorder**:
   - Set the "Save File Path" to your desired filename
   - Adjust the "Recording Interval" (default: 0.1 seconds)
   - Configure the "Play Area Bounds" to match your scene

2. **PlayMeowCatBehavior**:
   - Adjust movement parameters (speed, acceleration, turning)
   - Set engagement distance and distraction parameters

3. **PlayMeowLaserController**:
   - Set movement speed and play area bounds
   - Configure automatic movement parameters

4. **PlayMeowSimulationManager**:
   - Set simulation duration if desired (0 for unlimited)
   - Connect UI elements if using the UI Canvas

## Running the Simulation

1. Press the Play button in Unity to start the simulation
2. The cat will automatically follow the laser pointer
3. If using manual control, use WASD or arrow keys to move the laser pointer
4. If using automatic movement, the laser will move in the selected pattern

### Recording Data

1. Click the "Start Recording" button (or call StartRecording() via script)
2. The simulation will record data at the specified interval
3. Click "Stop Recording" to end recording and save data
4. The CSV file will be saved to the specified path in the persistent data directory

## CSV File Format

The generated CSV file will contain the following columns:

```
timestamp,cat_x,cat_y,cat_z,laser_x,laser_y,laser_z,engagement_indicator,time_since_engagement,distance_to_boundary_x,distance_to_boundary_z
```

This format is compatible with the PlayMeow data processing pipeline.

## Extending the Integration

You can extend this integration in several ways:

1. **Custom Cat Behavior**: Modify PlayMeowCatBehavior.cs to implement more realistic cat behavior
2. **Custom Laser Patterns**: Add new movement patterns to PlayMeowLaserController.cs
3. **Additional Features**: Extend PlayMeowDataRecorder.cs to record additional simulation data
4. **Real-time Visualization**: Add visualization tools to display engagement metrics and other data
5. **Physical Integration**: Use the Unity Environment with AR or physical robots

## Troubleshooting

### Common Issues:

1. **Missing References**: Ensure all object references are properly assigned in the Inspector
2. **Incorrect Bounds**: Verify that play area bounds are set correctly in both the laser controller and data recorder
3. **CSV Not Saving**: Check permissions and paths for the save location
4. **Performance Issues**: Reduce physics complexity or recording frequency if experiencing slowdowns

For more help, consult the PlayMeow project documentation or submit an issue to the repository.