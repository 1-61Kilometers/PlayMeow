import numpy as np
import tensorflow as tf
from tensorflow.keras.models import Sequential, clone_model
from tensorflow.keras.optimizers import Adam

class ReinforcementLearning:
    def __init__(self, model, gamma=0.99, replay_buffer_size=10000):
        """
        Initialize the reinforcement learning system.
        
        Args:
            model: The base model to use for RL
            gamma: Discount factor for future rewards
            replay_buffer_size: Size of experience replay buffer
        """
        self.base_model = model
        self.target_model = clone_model(model.model)
        self.target_model.set_weights(model.model.get_weights())
        
        self.gamma = gamma
        self.replay_buffer = []
        self.replay_buffer_size = replay_buffer_size
        
    def calculate_reward(self, engagement, distance, in_prohibited_zone, 
                         session_complete=False, session_abandoned=False):
        """
        Calculate reward based on cat engagement and other factors.
        
        Args:
            engagement: Binary engagement indicator
            distance: Distance between cat and laser
            in_prohibited_zone: Whether laser is in prohibited zone
            session_complete: Whether play session completed successfully
            session_abandoned: Whether play session was abandoned
            
        Returns:
            Calculated reward value
        """
        reward = 0
        
        # Engagement rewards
        if engagement:
            reward += 0.1  # High engagement
        else:
            reward -= 0.2  # Low engagement
        
        # Session completion rewards
        if session_complete:
            reward += 0.4
        elif session_abandoned:
            reward -= 0.3
        
        # Distance reward (appropriate distance maintenance)
        if 0.2 <= distance <= 1.0:  # Assuming units in meters
            reward += 0.01
        
        # Prohibited zone penalty
        if in_prohibited_zone:
            reward -= 0.1
            
        return reward
    
    def add_experience(self, state, action, reward, next_state, done):
        """
        Add experience to replay buffer.
        
        Args:
            state: Current state
            action: Action taken
            reward: Reward received
            next_state: Next state
            done: Whether episode is done
        """
        experience = (state, action, reward, next_state, done)
        
        # Add to replay buffer
        self.replay_buffer.append(experience)
        
        # Limit buffer size
        if len(self.replay_buffer) > self.replay_buffer_size:
            self.replay_buffer.pop(0)
    
    def train_on_batch(self, batch_size=32):
        """
        Train on a batch of experiences.
        
        Args:
            batch_size: Number of experiences to sample
            
        Returns:
            Loss value
        """
        if len(self.replay_buffer) < batch_size:
            return None
        
        # Sample batch of experiences
        indices = np.random.choice(len(self.replay_buffer), batch_size, replace=False)
        batch = [self.replay_buffer[i] for i in indices]
        
        states = np.array([exp[0] for exp in batch])
        actions = np.array([exp[1] for exp in batch])
        rewards = np.array([exp[2] for exp in batch])
        next_states = np.array([exp[3] for exp in batch])
        dones = np.array([exp[4] for exp in batch])
        
        # Get current Q values
        current_q = self.base_model.model.predict(states)
        
        # Get next Q values from target model
        next_q = self.target_model.predict(next_states)
        
        # Calculate target Q values
        target_q = current_q.copy()
        
        for i in range(batch_size):
            if dones[i]:
                target_q[i] = rewards[i]
            else:
                # Q-learning update rule
                target_q[i] = rewards[i] + self.gamma * np.max(next_q[i])
        
        # Train model on batch
        loss = self.base_model.model.train_on_batch(states, target_q)
        
        return loss
    
    def update_target_model(self, tau=0.1):
        """
        Update target model weights.
        
        Args:
            tau: Soft update parameter (0 < tau <= 1)
        """
        weights = self.base_model.model.get_weights()
        target_weights = self.target_model.get_weights()
        
        for i in range(len(weights)):
            target_weights[i] = tau * weights[i] + (1 - tau) * target_weights[i]
            
        self.target_model.set_weights(target_weights)
    
    def epsilon_greedy_action(self, state, epsilon=0.1):
        """
        Choose action using epsilon-greedy strategy.
        
        Args:
            state: Current state
            epsilon: Probability of random action
            
        Returns:
            Selected action
        """
        if np.random.random() < epsilon:
            # Random action: movement vector with random direction
            angle = np.random.uniform(0, 2 * np.pi)
            action = np.array([np.cos(angle), np.sin(angle)])
        else:
            # Greedy action
            action = self.base_model.predict(np.array([state]))[0]
            
        return action