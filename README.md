# PlayMeow: ML-driven Interactive Cat Laser Toy

PlayMeow is a machine learning system for a pet-interactive robotic laser toy that adapts to a cat's engagement patterns and preferences.

## Overview

The PlayMeow system processes positional time-series data from a cat and laser pointer to generate intelligent movement patterns that maintain the cat's engagement. The system uses a hybrid learning approach combining supervised learning from human demonstrations and reinforcement learning for continued adaptation.

## System Architecture

### Data Processing
- Processes 3D positional time-series data from cat and laser
- Uses sliding window approach (10 timesteps) for temporal data
- Calculates derived features including velocity and acceleration vectors
- Normalizes features to [-1, 1] range using StandardScaler
- Enables 5-timestep prediction horizons for anticipatory positioning

### Neural Network Architecture
- Feed-forward neural network with 3 hidden layers (128, 256, 128 units)
- ReLU activation for hidden layers, tanh for output layer
- Input features:
  * Cat's position (X,Z) relative to current laser position
  * Cat's velocity vector (speed and direction)
  * Binary engagement indicator
  * Time since last successful engagement
  * Distance from laser to environmental boundaries
- Output: 2 neurons representing movement vectors for X and Z

### Hybrid Learning Approach
1. **Behavioral Cloning**: Supervised learning from human demonstrations
2. **Simulation Training**: Train on recorded data in simulation
3. **Reinforcement Learning**: Framework for continued learning

### Reward System
- High engagement detection: +0.1
- Low engagement periods: -0.2
- Successful play session completion: +0.4
- Premature session abandonment: -0.3
- Maintaining appropriate distance: +0.01 per second
- Operating in prohibited zones: -0.1 per occurrence

## Installation

```bash
# Clone the repository
git clone https://github.com/your-username/playmeow.git
cd playmeow

# Install required packages
pip install -r requirements.txt
```

## Usage

### Command Line Interface

```bash
# Train with supervised learning only
python -m playmeow.main train --data-path playmeow/data/sample_data.csv --model-path playmeow/models/playmeow_model.h5

# Train with supervised learning followed by reinforcement learning
python -m playmeow.main train --data-path playmeow/data/sample_data.csv --model-path playmeow/models/playmeow_model.h5 --train-rl --rl-episodes 500

# Run simulation with a trained model
python -m playmeow.main simulate --model-path playmeow/models/playmeow_model.h5 --sim-episodes 10

# Start the Unity integration server
python -m playmeow.main unity --model-path playmeow/models/playmeow_model.h5 --port 12345

# Record data from Unity sessions
python -m playmeow.main unity --model-path playmeow/models/playmeow_model.h5 --record-data
```

### Web Interface

PlayMeow now includes a web dashboard for training, simulation, and visualization:

```bash
# Start the web server
python -m webapp.app
```

Then open your browser to http://localhost:5000

#### Web Interface Features

- **Dashboard**: Overview of the PlayMeow system
- **Training**: Train models with behavioral cloning and reinforcement learning
- **Simulation**: Run and visualize simulations of cat engagement
- **Visualization**: Explore trajectories and training metrics

## Data Format

The system expects CSV files with the following columns:
- `timestamp`: Time in seconds
- `cat_x`, `cat_y`, `cat_z`: Cat position in 3D space
- `laser_x`, `laser_y`, `laser_z`: Laser position in 3D space
- `engagement_indicator`: Binary indicator of cat engagement
- `time_since_engagement`: Time (in seconds) since last engagement
- `distance_to_boundary_x`, `distance_to_boundary_z`: Distance to environmental boundaries

## Unity Integration

The system provides a socket-based integration with Unity:
1. Unity sends JSON data with cat and laser positions
2. The ML model processes the data and predicts movement vectors
3. The server sends back movement commands to Unity

## Project Structure

- `playmeow/data_processor.py`: Data preprocessing and feature engineering
- `playmeow/model.py`: Neural network model definition
- `playmeow/trainer.py`: Training pipeline (supervised and reinforcement)
- `playmeow/reinforcement.py`: Reinforcement learning components
- `playmeow/simulation.py`: Simulation environment for training
- `playmeow/unity_integration.py`: Unity communication interface
- `playmeow/main.py`: Main entry point for the system
- `webapp/app.py`: Flask web interface
- `webapp/templates/`: HTML templates for web interface
- `webapp/static/`: CSS and JavaScript for web interface

## Requirements

- Python 3.7+
- TensorFlow 2.4+
- NumPy
- pandas
- scikit-learn
- Flask (for web interface)
- Plotly (for web visualizations)

## License

MIT