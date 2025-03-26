import os
import argparse
import numpy as np
import tensorflow as tf
import logging
from pathlib import Path

from playmeow.config import get_config, load_config, save_config
from playmeow.data_processor import DataProcessor
from playmeow.model import PlayMeowModel
from playmeow.trainer import PlayMeowTrainer
from playmeow.simulation import PlayMeowSimulation
from playmeow.reinforcement import ReinforcementLearning
from playmeow.unity_integration import UnityIntegration

# Get logger
logger = logging.getLogger(__name__)

def train_model(args):
    """
    Train the PlayMeow model.
    
    Args:
        args: Command-line arguments
    """
    config = get_config()
    
    # Load custom config if provided
    if args.config:
        load_config(args.config)
        config = get_config()
    
    logger.info(f"Training model using data from {args.data_path}")
    
    try:
        # Initialize trainer
        window_size = config['common']['window_size']
        prediction_horizon = config['common']['prediction_horizon']
        trainer = PlayMeowTrainer(window_size=window_size, prediction_horizon=prediction_horizon)
        
        # Prepare data
        X_train, X_test, y_train, y_test = trainer.prepare_data(args.data_path)
        
        logger.info(f"Training data shape: {X_train.shape}")
        logger.info(f"Testing data shape: {X_test.shape}")
        
        # Train with behavioral cloning
        batch_size = config['training']['batch_size']
        epochs = config['training']['max_epochs']
        history = trainer.train_behavioral_cloning(
            X_train, y_train,
            batch_size=batch_size,
            epochs=epochs,
            cross_val=args.cross_val
        )
        
        # Evaluate model
        metrics = trainer.evaluate_model(X_test, y_test)
        logger.info(f"Model evaluation metrics:")
        for key, value in metrics.items():
            logger.info(f"  {key}: {value:.4f}")
        
        # Create model directory if it doesn't exist
        os.makedirs(os.path.dirname(args.model_path), exist_ok=True)
        
        # Save model
        trainer.save_model(args.model_path)
        logger.info(f"Model saved to {args.model_path}")
        
        if args.train_rl:
            logger.info("Starting reinforcement learning training...")
            
            # Create simulation environment
            bounds = config['simulation']['bounds']
            sim = PlayMeowSimulation(bounds=tuple(bounds))
            
            # Train with reinforcement learning
            rewards = trainer.train_reinforcement(
                sim,
                n_episodes=args.rl_episodes,
                batch_size=batch_size,
                initial_epsilon=config['reinforcement']['initial_epsilon'],
                final_epsilon=config['reinforcement']['final_epsilon'],
                epsilon_decay_steps=config['reinforcement']['epsilon_decay_steps']
            )
            
            if rewards:
                logger.info(f"Reinforcement learning complete. Final average reward: {np.mean(rewards[-10:]):.2f}")
            else:
                logger.warning("No rewards received from reinforcement learning")
            
            # Save the RL-enhanced model
            rl_model_path = str(Path(args.model_path).with_suffix('')) + "_rl.h5"
            trainer.save_model(rl_model_path)
            logger.info(f"RL-enhanced model saved to {rl_model_path}")
            
    except FileNotFoundError as e:
        logger.error(f"File not found: {e}")
        raise
    except Exception as e:
        logger.error(f"Error during training: {e}")
        raise

def run_simulation(args):
    """
    Run a simulation of the PlayMeow system.
    
    Args:
        args: Command-line arguments
    """
    config = get_config()
    
    # Load custom config if provided
    if args.config:
        load_config(args.config)
        config = get_config()
    
    logger.info(f"Loading model from {args.model_path}")
    
    try:
        # Load trained model
        window_size = config['common']['window_size']
        input_shape = tuple(config['common']['input_shape'])
        
        trainer = PlayMeowTrainer(window_size=window_size)
        trainer.load_model(args.model_path, input_shape)
        
        # Create simulation
        bounds = tuple(config['simulation']['bounds'])
        cat_speed_range = tuple(config['simulation']['cat_speed_range'])
        engagement_threshold = config['simulation']['engagement_threshold']
        max_steps = config['simulation']['max_episode_steps']
        
        sim = PlayMeowSimulation(
            bounds=bounds,
            cat_speed_range=cat_speed_range,
            engagement_threshold=engagement_threshold
        )
        sim.max_episode_steps = max_steps
        
        # Run simulation episodes
        for episode in range(args.sim_episodes):
            try:
                state = sim.reset()
                done = False
                total_reward = 0
                steps = 0
                
                while not done and steps < max_steps:
                    # Get model prediction
                    action = trainer.model.predict(np.array([state]))[0]
                    
                    # Take action in simulation
                    next_state, reward, done, info = sim.step(action)
                    
                    # Update state and accumulate reward
                    state = next_state
                    total_reward += reward
                    steps += 1
                    
                    # Log info periodically
                    if steps % 50 == 0:
                        logger.info(f"Episode {episode+1}, Step {steps}, "
                                  f"Reward: {reward:.2f}, "
                                  f"Total: {total_reward:.2f}, "
                                  f"Engagement: {info['engagement']}, "
                                  f"Interest: {info['cat_interest_level']:.2f}")
                
                logger.info(f"Episode {episode+1} complete: "
                          f"Steps: {steps}, "
                          f"Total reward: {total_reward:.2f}, "
                          f"Complete: {info.get('session_complete', False)}, "
                          f"Abandoned: {info.get('session_abandoned', False)}")
                
            except Exception as e:
                logger.error(f"Error in simulation episode {episode+1}: {e}")
                continue
                
    except FileNotFoundError as e:
        logger.error(f"Model file not found: {e}")
        raise
    except Exception as e:
        logger.error(f"Error running simulation: {e}")
        raise

def run_unity_integration(args):
    """
    Run the Unity integration server.
    
    Args:
        args: Command-line arguments
    """
    config = get_config()
    
    # Load custom config if provided
    if args.config:
        load_config(args.config)
        config = get_config()
    
    logger.info(f"Starting Unity integration using model {args.model_path}")
    
    try:
        # Load trained model
        input_shape = tuple(config['common']['input_shape'])
        
        trainer = PlayMeowTrainer()
        trainer.load_model(args.model_path, input_shape)
        
        # Get Unity integration settings
        host = args.host or config['unity_integration']['host']
        port = args.port or config['unity_integration']['port']
        
        # Create and start Unity integration
        integration = UnityIntegration(trainer.model, host=host, port=port)
        
        try:
            integration.start()
            logger.info(f"Integration server started on {host}:{port}")
            logger.info("Press Ctrl+C to stop")
            
            # Keep running until interrupted
            while True:
                import time
                time.sleep(1)
                
        except KeyboardInterrupt:
            logger.info("\nStopping integration server...")
        finally:
            integration.stop()
            
            # Save recorded data if requested
            if args.record_data:
                # Create data directory if it doesn't exist
                data_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "data")
                os.makedirs(data_dir, exist_ok=True)
                
                # Generate unique filename with timestamp
                timestamp = int(time.time())
                output_path = os.path.join(data_dir, f"recorded_{timestamp}.csv")
                
                # Save data
                success = integration.save_recorded_data(output_path)
                if success:
                    logger.info(f"Recorded data saved to {output_path}")
                else:
                    logger.warning("Failed to save recorded data")
    
    except FileNotFoundError as e:
        logger.error(f"Model file not found: {e}")
        raise
    except Exception as e:
        logger.error(f"Error running Unity integration: {e}")
        raise

def main():
    # Load default config
    config = get_config()
    
    parser = argparse.ArgumentParser(description="PlayMeow Robotic Laser Toy ML System")
    
    # Global arguments for all commands
    parser.add_argument("--config", type=str, help="Path to configuration file")
    parser.add_argument("--log-level", type=str, choices=["DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"],
                        default="INFO", help="Logging level")
    
    subparsers = parser.add_subparsers(dest="command", help="Command to run")
    
    # Train command
    train_parser = subparsers.add_parser("train", help="Train the model")
    train_parser.add_argument("--data-path", type=str, 
                             default=config['common']['default_data_path'],
                             help="Path to training data CSV")
    train_parser.add_argument("--model-path", type=str, 
                             default=config['common']['default_model_path'],
                             help="Path to save the trained model")
    train_parser.add_argument("--train-rl", action="store_true",
                             help="Continue training with reinforcement learning")
    train_parser.add_argument("--rl-episodes", type=int, 
                             default=config['reinforcement']['epsilon_decay_steps'],
                             help="Number of episodes for RL training")
    train_parser.add_argument("--cross-val", action="store_true", default=True,
                             help="Use cross-validation during training")
    
    # Simulation command
    sim_parser = subparsers.add_parser("simulate", help="Run simulation")
    sim_parser.add_argument("--model-path", type=str, 
                           default=config['common']['default_model_path'],
                           help="Path to trained model")
    sim_parser.add_argument("--sim-episodes", type=int, default=10,
                           help="Number of simulation episodes to run")
    
    # Unity integration command
    unity_parser = subparsers.add_parser("unity", help="Run Unity integration server")
    unity_parser.add_argument("--model-path", type=str, 
                             default=config['common']['default_model_path'],
                             help="Path to trained model")
    unity_parser.add_argument("--host", type=str,
                             help=f"Host to run the integration server on (default: {config['unity_integration']['host']})")
    unity_parser.add_argument("--port", type=int,
                             help=f"Port to run the integration server on (default: {config['unity_integration']['port']})")
    unity_parser.add_argument("--record-data", action="store_true",
                             help="Record data from Unity sessions")
    
    # Config management commands
    config_parser = subparsers.add_parser("config", help="Manage configuration")
    config_subparsers = config_parser.add_subparsers(dest="config_command")
    
    # Save default config
    save_config_parser = config_subparsers.add_parser("save", help="Save current configuration to file")
    save_config_parser.add_argument("--path", type=str, default="playmeow_config.json",
                                  help="Path to save the configuration file")
    
    # View config
    view_config_parser = config_subparsers.add_parser("view", help="View current configuration")
    
    args = parser.parse_args()
    
    # Set logging level
    if hasattr(args, 'log_level'):
        numeric_level = getattr(logging, args.log_level.upper(), None)
        if numeric_level is not None:
            logging.getLogger().setLevel(numeric_level)
    
    # Create necessary directories
    if hasattr(args, 'model_path') and args.model_path:
        os.makedirs(os.path.dirname(args.model_path), exist_ok=True)
    else:
        os.makedirs(os.path.dirname(config['common']['default_model_path']), exist_ok=True)
    
    try:
        # Execute selected command
        if args.command == "train":
            train_model(args)
        elif args.command == "simulate":
            run_simulation(args)
        elif args.command == "unity":
            run_unity_integration(args)
        elif args.command == "config":
            if args.config_command == "save":
                save_config(args.path)
                logger.info(f"Configuration saved to {args.path}")
            elif args.config_command == "view":
                import json
                print(json.dumps(get_config(), indent=4))
            else:
                config_parser.print_help()
        else:
            parser.print_help()
    except Exception as e:
        logger.error(f"Error executing command: {e}")
        raise

if __name__ == "__main__":
    main()