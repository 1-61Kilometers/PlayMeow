import os
import json
import numpy as np
import pandas as pd
import plotly
import plotly.express as px
import plotly.graph_objects as go
from flask import Flask, render_template, request, jsonify, redirect, url_for, flash
from threading import Thread
import time
import sys
import logging
import uuid
from pathlib import Path

# Add parent directory to path for imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from playmeow.data_processor import DataProcessor
from playmeow.model import PlayMeowModel
from playmeow.trainer import PlayMeowTrainer
from playmeow.simulation import PlayMeowSimulation
from playmeow.reinforcement import ReinforcementLearning
from playmeow.config import get_config, load_config

# Configure logging
logging.basicConfig(
    level=logging.INFO, 
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler('webapp.log')
    ]
)
logger = logging.getLogger(__name__)

# Initialize Flask app
app = Flask(__name__)

# Load configuration
config = get_config()

# Set secret key from environment variable or config file
# If neither is available, generate a random key (but it won't persist across restarts)
app.secret_key = os.environ.get(
    'PLAYMEOW_SECRET_KEY',  # Check environment variable first
    config.get('webapp', {}).get('secret_key', str(uuid.uuid4()))  # Fall back to config or generate random key
)

# Log warning if using default or generated key in production
if app.secret_key == 'playmeow_flask_secret_key' or len(app.secret_key) < 24:
    if os.environ.get('FLASK_ENV') == 'production':
        logger.warning(
            "Using default or weak secret key in production! "
            "Set the PLAYMEOW_SECRET_KEY environment variable with a strong secret"
        )

# Global variables
simulation_results = []
current_simulation = None
simulation_thread = None
is_simulation_running = False
current_training = None
is_training = False
training_thread = None
trainer = None

# Get configuration
DEFAULT_MODEL_PATH = config['common']['default_model_path']
DEFAULT_DATA_PATH = config['common']['default_data_path']
INPUT_SHAPE = tuple(config['common']['input_shape']) 
WINDOW_SIZE = config['common']['window_size']
PREDICTION_HORIZON = config['common']['prediction_horizon']

def initialize_trainer():
    """Initialize the trainer with default settings"""
    global trainer
    
    try:
        if trainer is None:
            trainer = PlayMeowTrainer(window_size=WINDOW_SIZE, prediction_horizon=PREDICTION_HORIZON)
            # Try to load model if it exists
            if os.path.exists(DEFAULT_MODEL_PATH):
                try:
                    trainer.load_model(DEFAULT_MODEL_PATH, INPUT_SHAPE)
                    logger.info(f"Loaded model from {DEFAULT_MODEL_PATH}")
                except Exception as e:
                    logger.error(f"Error loading model: {e}")
                    logger.info("Initializing with new model")
    except Exception as e:
        logger.error(f"Error initializing trainer: {e}")
        # Create empty trainer as fallback
        trainer = PlayMeowTrainer()

# Initialize on startup
initialize_trainer()

@app.route('/')
def index():
    """Render the main dashboard"""
    return render_template('index.html', 
                          simulation_running=is_simulation_running,
                          training_running=is_training)

@app.route('/train', methods=['GET', 'POST'])
def train():
    """Train model page and API endpoint"""
    global is_training, training_thread, current_training, trainer
    
    if request.method == 'POST':
        data_path = request.form.get('data_path', DEFAULT_DATA_PATH)
        model_path = request.form.get('model_path', DEFAULT_MODEL_PATH)
        batch_size = int(request.form.get('batch_size', 32))
        epochs = int(request.form.get('epochs', 100))
        use_cross_val = request.form.get('cross_val') == 'on'
        use_rl = request.form.get('train_rl') == 'on'
        rl_episodes = int(request.form.get('rl_episodes', 100))
        
        # Make sure directories exist
        os.makedirs(os.path.dirname(model_path), exist_ok=True)
        
        if is_training:
            flash('Training is already in progress.', 'warning')
            return redirect(url_for('train'))
        
        def training_worker():
            global is_training, current_training, trainer
            
            try:
                # Initialize trainer if needed
                if trainer is None:
                    trainer = PlayMeowTrainer(window_size=10, prediction_horizon=5)
                
                current_training = {
                    'status': 'preparing_data',
                    'progress': 0,
                    'metrics': {},
                    'history': [],
                    'rewards': []
                }
                
                # Prepare data
                logger.info(f"Preparing data from {data_path}")
                X_train, X_test, y_train, y_test = trainer.prepare_data(data_path)
                
                current_training['status'] = 'training_supervised'
                logger.info(f"Starting supervised training with {len(X_train)} samples")
                
                # Train with behavioral cloning
                history = trainer.train_behavioral_cloning(
                    X_train, y_train,
                    batch_size=batch_size,
                    epochs=epochs,
                    cross_val=use_cross_val
                )
                
                # Store training history
                if isinstance(history, list):  # Cross-validation histories
                    for i, h in enumerate(history):
                        current_training['history'].append({
                            'fold': i+1,
                            'loss': h.history.get('loss', []),
                            'val_loss': h.history.get('val_loss', [])
                        })
                else:
                    current_training['history'].append({
                        'fold': 0,
                        'loss': history.history.get('loss', []),
                        'val_loss': history.history.get('val_loss', [])
                    })
                
                # Evaluate model
                current_training['status'] = 'evaluating'
                metrics = trainer.evaluate_model(X_test, y_test)
                current_training['metrics'] = metrics
                
                # Save model
                trainer.save_model(model_path)
                logger.info(f"Model saved to {model_path}")
                
                # Reinforcement learning if selected
                if use_rl:
                    current_training['status'] = 'training_rl'
                    logger.info("Starting reinforcement learning training...")
                    
                    # Create simulation environment
                    sim = PlayMeowSimulation(bounds=(-2, 2, -2, 2))
                    
                    # Prohibited zones have been removed from the simulation
                    
                    # Train with reinforcement learning
                    rewards = trainer.train_reinforcement(
                        sim,
                        n_episodes=rl_episodes,
                        batch_size=batch_size
                    )
                    
                    current_training['rewards'] = rewards
                    
                    # Save the RL-enhanced model
                    rl_model_path = model_path.replace('.h5', '_rl.h5')
                    trainer.save_model(rl_model_path)
                    logger.info(f"RL-enhanced model saved to {rl_model_path}")
                
                current_training['status'] = 'completed'
                logger.info("Training completed successfully")
                
            except Exception as e:
                logger.error(f"Error during training: {e}")
                current_training['status'] = 'error'
                current_training['error'] = str(e)
            finally:
                is_training = False
        
        # Start training in background thread
        is_training = True
        training_thread = Thread(target=training_worker)
        training_thread.daemon = True
        training_thread.start()
        
        flash('Training started in the background. Check the "Training Status" section for updates.', 'success')
        return redirect(url_for('train'))
    
    # GET request - display the training page
    return render_template('train.html', 
                          training_running=is_training,
                          training_status=current_training)

@app.route('/training/status')
def training_status():
    """API endpoint for getting training status"""
    global current_training
    if current_training is None:
        return jsonify({'status': 'not_started'})
    return jsonify(current_training)

@app.route('/simulate', methods=['GET', 'POST'])
def simulate():
    """Simulation page and API endpoint"""
    global is_simulation_running, simulation_thread, current_simulation, trainer
    
    if request.method == 'POST':
        model_path = request.form.get('model_path', DEFAULT_MODEL_PATH)
        num_episodes = int(request.form.get('episodes', 10))
        
        if is_simulation_running:
            flash('Simulation is already running.', 'warning')
            return redirect(url_for('simulate'))
        
        def simulation_worker():
            global is_simulation_running, current_simulation, simulation_results, trainer
            
            try:
                # Initialize trainer if needed
                if trainer is None:
                    trainer = PlayMeowTrainer()
                
                # Load model if different from currently loaded
                try:
                    trainer.load_model(model_path, INPUT_SHAPE)
                    logger.info(f"Loaded model from {model_path}")
                except Exception as e:
                    logger.error(f"Error loading model: {e}")
                    current_simulation = {
                        'status': 'error',
                        'error': f"Failed to load model: {str(e)}"
                    }
                    is_simulation_running = False
                    return
                
                # Create simulation
                sim = PlayMeowSimulation(bounds=(-2, 2, -2, 2))
                
                # Prohibited zones have been removed from the simulation
                
                # Clear previous results
                simulation_results = []
                
                # Initialize current simulation status
                current_simulation = {
                    'status': 'running',
                    'episode': 0,
                    'total_episodes': num_episodes,
                    'current_step': 0,
                    'total_reward': 0,
                    'trajectory': [],
                    'metrics': {}
                }
                
                # Run simulation episodes
                all_episode_rewards = []
                
                for episode in range(num_episodes):
                    state = sim.reset()
                    done = False
                    total_reward = 0
                    steps = 0
                    episode_trajectory = []
                    
                    current_simulation['episode'] = episode + 1
                    current_simulation['current_step'] = 0
                    current_simulation['total_reward'] = 0
                    
                    while not done and steps < 300:  # Max 300 steps per episode
                        # Get model prediction
                        action = trainer.model.predict(np.array([state]))[0]
                        
                        # Take action in simulation
                        next_state, reward, done, info = sim.step(action)
                        
                        # Add to trajectory
                        episode_trajectory.append({
                            'x': float(sim.cat_pos[0]),
                            'y': float(sim.cat_pos[1]),
                            'reward': float(reward),
                            'engagement': float(info['engagement']),
                            'interest': float(info['cat_interest_level'])
                        })
                        
                        # Update state and accumulate reward
                        state = next_state
                        total_reward += reward
                        steps += 1
                        
                        current_simulation['current_step'] = steps
                        current_simulation['total_reward'] = total_reward
                        current_simulation['trajectory'] = episode_trajectory
                        
                        # Slow down simulation for visualization
                        time.sleep(0.05)
                    
                    # Episode complete - save results
                    episode_result = {
                        'episode': episode + 1,
                        'steps': steps,
                        'total_reward': total_reward,
                        'complete': info.get('session_complete', False),
                        'abandoned': info.get('session_abandoned', False),
                        'trajectory': episode_trajectory
                    }
                    
                    simulation_results.append(episode_result)
                    all_episode_rewards.append(total_reward)
                    
                    logger.info(f"Episode {episode+1} complete: "
                            f"Steps: {steps}, "
                            f"Total reward: {total_reward:.2f}")
                
                # All episodes complete
                current_simulation['status'] = 'completed'
                current_simulation['metrics'] = {
                    'avg_reward': float(np.mean(all_episode_rewards)),
                    'max_reward': float(np.max(all_episode_rewards)),
                    'min_reward': float(np.min(all_episode_rewards)),
                    'std_reward': float(np.std(all_episode_rewards))
                }
                logger.info("Simulation completed successfully")
                
            except Exception as e:
                logger.error(f"Error during simulation: {e}")
                current_simulation['status'] = 'error'
                current_simulation['error'] = str(e)
            finally:
                is_simulation_running = False
        
        # Start simulation in background thread
        is_simulation_running = True
        simulation_thread = Thread(target=simulation_worker)
        simulation_thread.daemon = True
        simulation_thread.start()
        
        flash('Simulation started in the background. Check the "Simulation Status" section for updates.', 'success')
        return redirect(url_for('simulate'))
    
    # GET request - display the simulation page
    return render_template('simulate.html', 
                          simulation_running=is_simulation_running,
                          simulation_status=current_simulation,
                          simulation_results=simulation_results)

@app.route('/simulation/status')
def simulation_status():
    """API endpoint for getting simulation status"""
    global current_simulation
    if current_simulation is None:
        return jsonify({'status': 'not_started'})
    return jsonify(current_simulation)

@app.route('/simulation/results')
def simulation_results_api():
    """API endpoint for getting simulation results"""
    global simulation_results
    return jsonify(simulation_results)

@app.route('/visualization')
def visualization():
    """Visualization page for simulation results and training metrics"""
    return render_template('visualization.html',
                          simulation_results=simulation_results,
                          training_status=current_training)

@app.route('/plot/training')
def plot_training():
    """Generate training loss plot"""
    if current_training is None or not current_training.get('history'):
        return jsonify({'error': 'No training data available'})
    
    # Create figure for loss curves
    fig = go.Figure()
    
    for h in current_training['history']:
        fold_num = h.get('fold', 0)
        epochs = list(range(1, len(h['loss'])+1))
        
        # Add training loss
        if h.get('loss'):
            fig.add_trace(go.Scatter(
                x=epochs, 
                y=h['loss'],
                mode='lines',
                name=f'Fold {fold_num} - Training Loss' if fold_num > 0 else 'Training Loss'
            ))
        
        # Add validation loss if available
        if h.get('val_loss'):
            fig.add_trace(go.Scatter(
                x=epochs, 
                y=h['val_loss'],
                mode='lines',
                name=f'Fold {fold_num} - Validation Loss' if fold_num > 0 else 'Validation Loss',
                line=dict(dash='dash')
            ))
    
    fig.update_layout(
        title='Training and Validation Loss',
        xaxis_title='Epoch',
        yaxis_title='Loss',
        template='plotly_white'
    )
    
    return jsonify(plotly.io.to_json(fig))

@app.route('/plot/rewards')
def plot_rewards():
    """Generate RL rewards plot"""
    if current_training is None or not current_training.get('rewards'):
        return jsonify({'error': 'No rewards data available'})
    
    rewards = current_training['rewards']
    episodes = list(range(1, len(rewards)+1))
    
    fig = go.Figure()
    
    # Add raw rewards
    fig.add_trace(go.Scatter(
        x=episodes,
        y=rewards,
        mode='lines',
        name='Episode Rewards',
        line=dict(color='royalblue')
    ))
    
    # Add moving average
    window = min(10, len(rewards))
    if window > 0:
        moving_avg = [np.mean(rewards[max(0, i-window):i+1]) for i in range(len(rewards))]
        fig.add_trace(go.Scatter(
            x=episodes,
            y=moving_avg,
            mode='lines',
            name='10-Episode Moving Average',
            line=dict(color='firebrick', width=2)
        ))
    
    fig.update_layout(
        title='Reinforcement Learning Rewards',
        xaxis_title='Episode',
        yaxis_title='Reward',
        template='plotly_white'
    )
    
    return jsonify(plotly.io.to_json(fig))

@app.route('/plot/trajectory/<int:episode>')
def plot_trajectory(episode):
    """Generate trajectory plot for a specific episode"""
    global simulation_results
    
    # Make episode 1-indexed to 0-indexed
    episode_idx = episode - 1
    
    if not simulation_results or episode_idx < 0 or episode_idx >= len(simulation_results):
        return jsonify({'error': 'Episode data not available'})
    
    episode_data = simulation_results[episode_idx]
    trajectory = episode_data.get('trajectory', [])
    
    if not trajectory:
        return jsonify({'error': 'No trajectory data for this episode'})
    
    # Extract coordinates
    x_coords = [point['x'] for point in trajectory]
    y_coords = [point['y'] for point in trajectory]
    rewards = [point['reward'] for point in trajectory]
    
    fig = go.Figure()
    
    # Add trajectory line
    fig.add_trace(go.Scatter(
        x=x_coords,
        y=y_coords,
        mode='lines+markers',
        marker=dict(
            size=8,
            color=rewards,
            colorscale='RdYlGn',
            colorbar=dict(
                title='Reward',
                # Adjust colorbar position to avoid overlap
                x=1.05,  # Position it further right
                xpad=10,  # Add padding
                len=0.8,  # Make it 80% of the plot height
                y=0.5,   # Center it vertically
                yanchor='middle'
            ),
            showscale=True
        ),
        line=dict(width=2),
        name='Cat Path'
    ))
    
    # Prohibited zones have been removed from the visualization
    
    # Add start and end points
    fig.add_trace(go.Scatter(
        x=[x_coords[0]],
        y=[y_coords[0]],
        mode='markers',
        marker=dict(size=15, color='green', symbol='circle-open'),
        name='Start'
    ))
    
    fig.add_trace(go.Scatter(
        x=[x_coords[-1]],
        y=[y_coords[-1]],
        mode='markers',
        marker=dict(size=15, color='red', symbol='x'),
        name='End'
    ))
    
    # Update layout
    fig.update_layout(
        title=f'Episode {episode} Trajectory (Total Reward: {episode_data["total_reward"]:.2f})',
        xaxis_title='X Position',
        yaxis_title='Y Position',
        xaxis=dict(range=[-2.5, 2.5]),
        yaxis=dict(range=[-2.5, 2.5]),
        template='plotly_white',
        # Fix legend position to avoid overlap with colorbar
        legend=dict(
            orientation="h",
            yanchor="bottom",
            y=1.02,
            xanchor="right",
            x=1
        ),
        # Adjust margins to make room for legend and colorbar
        margin=dict(t=80, r=80, l=50, b=50)
    )
    
    return jsonify(plotly.io.to_json(fig))

if __name__ == '__main__':
    # Get host, port, and debug settings from config (with environment variable override)
    host = os.environ.get('PLAYMEOW_WEB_HOST', config['webapp']['host'])
    port = int(os.environ.get('PLAYMEOW_WEB_PORT', config['webapp']['port']))
    debug = os.environ.get('PLAYMEOW_WEB_DEBUG', str(config['webapp']['debug'])).lower() in ['true', '1', 'yes']
    
    # Start the Flask app
    logger.info(f"Starting web application on {host}:{port} (debug={debug})")
    app.run(debug=debug, host=host, port=port)