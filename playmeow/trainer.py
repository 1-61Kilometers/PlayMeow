import numpy as np
import os
import tensorflow as tf
from tensorflow.keras.optimizers import Adam
from sklearn.model_selection import train_test_split
from playmeow.data_processor import DataProcessor
from playmeow.model import PlayMeowModel
from playmeow.reinforcement import ReinforcementLearning

class PlayMeowTrainer:
    def __init__(self, window_size=10, prediction_horizon=5):
        """
        Initialize the PlayMeow trainer.
        
        Args:
            window_size: Size of sliding window
            prediction_horizon: How many steps ahead to predict
        """
        self.window_size = window_size
        self.prediction_horizon = prediction_horizon
        self.data_processor = DataProcessor(window_size, prediction_horizon)
        self.model = None
        self.rl_agent = None
        
    def prepare_data(self, data_filepath):
        """
        Prepare data for training.
        
        Args:
            data_filepath: Path to CSV data file
            
        Returns:
            Processed training and testing data
        """
        X_train, X_test, y_train, y_test = self.data_processor.preprocess_data(data_filepath)
        return X_train, X_test, y_train, y_test
    
    def train_behavioral_cloning(self, X_train, y_train, X_val=None, y_val=None,
                                batch_size=32, epochs=100, cross_val=True):
        """
        Train the model using behavioral cloning (supervised learning).
        
        Args:
            X_train: Training features
            y_train: Training targets
            X_val: Validation features (optional)
            y_val: Validation targets (optional)
            batch_size: Training batch size
            epochs: Maximum number of epochs
            cross_val: Whether to use cross-validation
            
        Returns:
            Training history
        """
        # Initialize model if not already created
        if self.model is None:
            input_shape = (self.window_size, X_train.shape[2])
            self.model = PlayMeowModel(input_shape)
        
        # Train using supervised learning
        history = self.model.train_supervised(
            X_train, y_train,
            X_val, y_val,
            batch_size, epochs,
            cross_val=cross_val
        )
        
        return history
    
    def train_reinforcement(self, simulation_env, n_episodes=1000, batch_size=32,
                           initial_epsilon=1.0, final_epsilon=0.1, 
                           epsilon_decay_steps=500):
        """
        Train the model using reinforcement learning.
        
        Args:
            simulation_env: Simulation environment for RL training
            n_episodes: Number of episodes to train
            batch_size: Training batch size
            initial_epsilon: Initial exploration rate
            final_epsilon: Final exploration rate
            epsilon_decay_steps: Steps over which to decay epsilon
            
        Returns:
            List of episode rewards
        """
        # Initialize RL agent if not already created
        if self.rl_agent is None:
            self.rl_agent = ReinforcementLearning(self.model)
        
        episode_rewards = []
        
        for episode in range(n_episodes):
            # Reset environment
            state = simulation_env.reset()
            done = False
            total_reward = 0
            
            # Calculate epsilon for this episode
            epsilon = max(
                final_epsilon,
                initial_epsilon - (initial_epsilon - final_epsilon) * episode / epsilon_decay_steps
            )
            
            while not done:
                # Choose action using epsilon-greedy strategy
                action = self.rl_agent.epsilon_greedy_action(state, epsilon)
                
                # Take action in environment
                next_state, reward, done, info = simulation_env.step(action)
                
                # Add experience to replay buffer
                self.rl_agent.add_experience(state, action, reward, next_state, done)
                
                # Train on batch of experiences
                loss = self.rl_agent.train_on_batch(batch_size)
                
                # Update target model occasionally
                if episode % 10 == 0:
                    self.rl_agent.update_target_model()
                
                # Update state and accumulate reward
                state = next_state
                total_reward += reward
            
            episode_rewards.append(total_reward)
            
            # Print progress
            if episode % 10 == 0:
                print(f"Episode {episode}/{n_episodes}, "
                      f"Reward: {total_reward:.2f}, "
                      f"Epsilon: {epsilon:.2f}")
        
        return episode_rewards
    
    def evaluate_model(self, X_test, y_test):
        """
        Evaluate the trained model.
        
        Args:
            X_test: Test features
            y_test: Test targets
            
        Returns:
            Dictionary of evaluation metrics
        """
        if self.model is None:
            raise ValueError("Model has not been trained yet")
        
        # Get predictions
        y_pred = self.model.predict(X_test)
        
        # Calculate MSE
        mse = np.mean(np.square(y_test - y_pred))
        
        # Calculate direction accuracy (if vectors point in similar direction)
        dot_products = np.sum(y_test * y_pred, axis=1)
        magnitudes_test = np.sqrt(np.sum(y_test**2, axis=1))
        magnitudes_pred = np.sqrt(np.sum(y_pred**2, axis=1))
        
        # Cosine similarity (avoid division by zero)
        magnitudes_product = magnitudes_test * magnitudes_pred
        cosine_similarities = np.zeros_like(magnitudes_product)
        nonzero_idx = magnitudes_product > 0
        cosine_similarities[nonzero_idx] = dot_products[nonzero_idx] / magnitudes_product[nonzero_idx]
        
        # Direction accuracy: cosine similarity > 0.7 (about 45 degrees)
        direction_accuracy = np.mean(cosine_similarities > 0.7)
        
        return {
            'mse': mse,
            'direction_accuracy': direction_accuracy,
            'cosine_similarity': np.mean(cosine_similarities)
        }
    
    def save_model(self, filepath):
        """
        Save the trained model.
        
        Args:
            filepath: Path to save the model
        """
        if self.model is None:
            raise ValueError("No model to save")
        
        self.model.save(filepath)
        
    def load_model(self, filepath, input_shape):
        """
        Load a trained model.
        
        Args:
            filepath: Path to the saved model
            input_shape: Shape of input data
        """
        self.model = PlayMeowModel(input_shape)
        self.model.load(filepath)