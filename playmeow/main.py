import os
import argparse
import numpy as np
import tensorflow as tf
from pathlib import Path

from playmeow.data_processor import DataProcessor
from playmeow.model import PlayMeowModel
from playmeow.trainer import PlayMeowTrainer
from playmeow.simulation import PlayMeowSimulation
from playmeow.reinforcement import ReinforcementLearning
from playmeow.unity_integration import UnityIntegration

def train_model(args):
    """
    Train the PlayMeow model.
    
    Args:
        args: Command-line arguments
    """
    print(f"Training model using data from {args.data_path}")
    
    # Initialize trainer
    trainer = PlayMeowTrainer(window_size=10, prediction_horizon=5)
    
    # Prepare data
    X_train, X_test, y_train, y_test = trainer.prepare_data(args.data_path)
    
    print(f"Training data shape: {X_train.shape}")
    print(f"Testing data shape: {X_test.shape}")
    
    # Train with behavioral cloning
    history = trainer.train_behavioral_cloning(
        X_train, y_train,
        batch_size=32,
        epochs=100,
        cross_val=True
    )
    
    # Evaluate model
    metrics = trainer.evaluate_model(X_test, y_test)
    print(f"Model evaluation metrics:")
    for key, value in metrics.items():
        print(f"  {key}: {value:.4f}")
    
    # Save model
    trainer.save_model(args.model_path)
    print(f"Model saved to {args.model_path}")
    
    if args.train_rl:
        print("Starting reinforcement learning training...")
        
        # Create simulation environment
        sim = PlayMeowSimulation(bounds=(-2, 2, -2, 2))
        
        
        # Train with reinforcement learning
        rewards = trainer.train_reinforcement(
            sim,
            n_episodes=args.rl_episodes,
            batch_size=32
        )
        
        print(f"Reinforcement learning complete. Final average reward: {np.mean(rewards[-10:]):.2f}")
        
        # Save the RL-enhanced model
        rl_model_path = str(Path(args.model_path).with_suffix('')) + "_rl.h5"
        trainer.save_model(rl_model_path)
        print(f"RL-enhanced model saved to {rl_model_path}")

def run_simulation(args):
    """
    Run a simulation of the PlayMeow system.
    
    Args:
        args: Command-line arguments
    """
    print(f"Loading model from {args.model_path}")
    
    # Load trained model
    data_processor = DataProcessor()
    X_dummy = np.zeros((1, 10, 10))  # Dummy data to get input shape
    input_shape = (10, 10)  # Assuming 10 features and window size of 10
    
    trainer = PlayMeowTrainer()
    trainer.load_model(args.model_path, input_shape)
    
    # Create simulation
    sim = PlayMeowSimulation(bounds=(-2, 2, -2, 2))
    
    
    # Run simulation episodes
    for episode in range(args.sim_episodes):
        state = sim.reset()
        done = False
        total_reward = 0
        steps = 0
        
        while not done and steps < 300:  # Max 300 steps per episode
            # Get model prediction
            action = trainer.model.predict(np.array([state]))[0]
            
            # Take action in simulation
            next_state, reward, done, info = sim.step(action)
            
            # Update state and accumulate reward
            state = next_state
            total_reward += reward
            steps += 1
            
            # Print info every 50 steps
            if steps % 50 == 0:
                print(f"Episode {episode+1}, Step {steps}, "
                      f"Reward: {reward:.2f}, "
                      f"Total: {total_reward:.2f}, "
                      f"Engagement: {info['engagement']}, "
                      f"Interest: {info['cat_interest_level']:.2f}")
        
        print(f"Episode {episode+1} complete: "
              f"Steps: {steps}, "
              f"Total reward: {total_reward:.2f}, "
              f"Complete: {info.get('session_complete', False)}, "
              f"Abandoned: {info.get('session_abandoned', False)}")

def run_unity_integration(args):
    """
    Run the Unity integration server.
    
    Args:
        args: Command-line arguments
    """
    print(f"Starting Unity integration using model {args.model_path}")
    
    # Load trained model
    input_shape = (10, 10)  # Assuming 10 features and window size of 10
    
    trainer = PlayMeowTrainer()
    trainer.load_model(args.model_path, input_shape)
    
    # Create and start Unity integration
    integration = UnityIntegration(trainer.model, port=args.port)
    
    try:
        integration.start()
        print(f"Integration server started on port {args.port}")
        print("Press Ctrl+C to stop")
        
        # Keep running until interrupted
        while True:
            import time
            time.sleep(1)
            
    except KeyboardInterrupt:
        print("\nStopping integration server...")
    finally:
        integration.stop()
        
        # Save recorded data if requested
        if args.record_data:
            output_path = f"playmeow/data/recorded_{int(time.time())}.csv"
            integration.save_recorded_data(output_path)

def main():
    parser = argparse.ArgumentParser(description="PlayMeow Robotic Laser Toy ML System")
    subparsers = parser.add_subparsers(dest="command", help="Command to run")
    
    # Train command
    train_parser = subparsers.add_parser("train", help="Train the model")
    train_parser.add_argument("--data-path", type=str, default="playmeow/data/sample_data.csv",
                             help="Path to training data CSV")
    train_parser.add_argument("--model-path", type=str, default="playmeow/models/playmeow_model.h5",
                             help="Path to save the trained model")
    train_parser.add_argument("--train-rl", action="store_true",
                             help="Continue training with reinforcement learning")
    train_parser.add_argument("--rl-episodes", type=int, default=500,
                             help="Number of episodes for RL training")
    
    # Simulation command
    sim_parser = subparsers.add_parser("simulate", help="Run simulation")
    sim_parser.add_argument("--model-path", type=str, default="playmeow/models/playmeow_model.h5",
                           help="Path to trained model")
    sim_parser.add_argument("--sim-episodes", type=int, default=10,
                           help="Number of simulation episodes to run")
    
    # Unity integration command
    unity_parser = subparsers.add_parser("unity", help="Run Unity integration server")
    unity_parser.add_argument("--model-path", type=str, default="playmeow/models/playmeow_model.h5",
                             help="Path to trained model")
    unity_parser.add_argument("--port", type=int, default=12345,
                             help="Port to run the integration server on")
    unity_parser.add_argument("--record-data", action="store_true",
                             help="Record data from Unity sessions")
    
    args = parser.parse_args()
    
    # Create necessary directories
    os.makedirs(os.path.dirname(args.model_path if hasattr(args, 'model_path') else "playmeow/models/"), exist_ok=True)
    
    # Execute selected command
    if args.command == "train":
        train_model(args)
    elif args.command == "simulate":
        run_simulation(args)
    elif args.command == "unity":
        run_unity_integration(args)
    else:
        parser.print_help()

if __name__ == "__main__":
    main()