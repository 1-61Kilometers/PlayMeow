import numpy as np
import pandas as pd
from sklearn.preprocessing import StandardScaler

class DataProcessor:
    def __init__(self, window_size=10, prediction_horizon=5):
        """
        Initialize the data processor for PlayMeow.
        
        Args:
            window_size: Number of timesteps to include in each window
            prediction_horizon: Number of timesteps to predict ahead
        """
        self.window_size = window_size
        self.prediction_horizon = prediction_horizon
        self.scaler = StandardScaler()
        
    def load_data(self, filepath):
        """
        Load data from CSV file.
        
        Args:
            filepath: Path to the CSV file
            
        Returns:
            Loaded DataFrame
        """
        return pd.read_csv(filepath)
    
    def calculate_derived_features(self, df):
        """
        Calculate velocity and acceleration vectors.
        
        Args:
            df: DataFrame with position data
            
        Returns:
            DataFrame with additional velocity and acceleration columns
        """
        # Calculate cat velocity
        df['cat_velocity_x'] = df['cat_x'].diff() / df['timestamp'].diff()
        df['cat_velocity_z'] = df['cat_z'].diff() / df['timestamp'].diff()
        
        # Calculate cat speed (magnitude of velocity)
        df['cat_speed'] = np.sqrt(df['cat_velocity_x']**2 + df['cat_velocity_z']**2)
        
        # Calculate cat direction (angle of velocity)
        df['cat_direction'] = np.arctan2(df['cat_velocity_z'], df['cat_velocity_x'])
        
        # Calculate cat acceleration
        df['cat_accel_x'] = df['cat_velocity_x'].diff() / df['timestamp'].diff()
        df['cat_accel_z'] = df['cat_velocity_z'].diff() / df['timestamp'].diff()
        
        # Calculate cat position relative to laser
        df['cat_rel_x'] = df['cat_x'] - df['laser_x']
        df['cat_rel_z'] = df['cat_z'] - df['laser_z']
        
        # Calculate distance between cat and laser
        df['cat_laser_distance'] = np.sqrt(df['cat_rel_x']**2 + df['cat_rel_z']**2)
        
        # Drop rows with NaN values (from diff calculations)
        return df.dropna().reset_index(drop=True)
    
    def create_sliding_windows(self, df):
        """
        Create sliding windows from time series data.
        
        Args:
            df: DataFrame with feature columns
            
        Returns:
            X: Input features for each window
            y: Target outputs (future positions) for each window
        """
        features = [
            'cat_rel_x', 'cat_rel_z',
            'cat_velocity_x', 'cat_velocity_z',
            'cat_speed', 'cat_direction',
            'engagement_indicator',
            'time_since_engagement',
            'distance_to_boundary_x', 'distance_to_boundary_z'
        ]
        
        X = []
        y = []
        
        for i in range(len(df) - self.window_size - self.prediction_horizon):
            # Input window
            window = df.iloc[i:i+self.window_size][features].values
            
            # Target: future movement vector (normalized to [-1, 1])
            future_pos = df.iloc[i+self.window_size+self.prediction_horizon][['laser_x', 'laser_z']].values
            current_pos = df.iloc[i+self.window_size][['laser_x', 'laser_z']].values
            movement_vector = future_pos - current_pos
            
            # Normalize movement vector to [-1, 1]
            max_move = max(abs(movement_vector))
            if max_move > 0:
                movement_vector = movement_vector / max_move
            
            X.append(window)
            y.append(movement_vector)
            
        return np.array(X), np.array(y)
    
    def normalize_features(self, X_train, X_test=None):
        """
        Normalize features to [-1, 1] range.
        
        Args:
            X_train: Training data to fit scaler
            X_test: Optional test data to transform
            
        Returns:
            X_train_norm: Normalized training data
            X_test_norm: Normalized test data (if provided)
        """
        # Reshape to 2D array for StandardScaler
        n_samples, n_timesteps, n_features = X_train.shape
        X_train_reshaped = X_train.reshape(n_samples, n_timesteps * n_features)
        
        # Fit scaler on training data
        self.scaler.fit(X_train_reshaped)
        
        # Transform training data
        X_train_scaled = self.scaler.transform(X_train_reshaped)
        X_train_norm = X_train_scaled.reshape(n_samples, n_timesteps, n_features)
        
        # Rescale to [-1, 1] range
        X_train_norm = 2.0 * (X_train_norm - np.min(X_train_norm)) / (np.max(X_train_norm) - np.min(X_train_norm)) - 1.0
        
        if X_test is not None:
            # Reshape test data
            n_samples_test = X_test.shape[0]
            X_test_reshaped = X_test.reshape(n_samples_test, n_timesteps * n_features)
            
            # Transform test data
            X_test_scaled = self.scaler.transform(X_test_reshaped)
            X_test_norm = X_test_scaled.reshape(n_samples_test, n_timesteps, n_features)
            
            # Rescale to [-1, 1] range
            X_test_norm = 2.0 * (X_test_norm - np.min(X_test_norm)) / (np.max(X_test_norm) - np.min(X_test_norm)) - 1.0
            
            return X_train_norm, X_test_norm
        
        return X_train_norm
    
    def preprocess_data(self, filepath, test_split=0.2):
        """
        Full preprocessing pipeline.
        
        Args:
            filepath: Path to CSV data file
            test_split: Proportion of data to use for testing
            
        Returns:
            X_train, X_test, y_train, y_test: Processed and normalized data
        """
        # Load data
        df = self.load_data(filepath)
        
        # Calculate derived features
        df = self.calculate_derived_features(df)
        
        # Create sliding windows
        X, y = self.create_sliding_windows(df)
        
        # Split into train and test sets
        split_idx = int(len(X) * (1 - test_split))
        X_train, X_test = X[:split_idx], X[split_idx:]
        y_train, y_test = y[:split_idx], y[split_idx:]
        
        # Normalize features
        X_train_norm, X_test_norm = self.normalize_features(X_train, X_test)
        
        return X_train_norm, X_test_norm, y_train, y_test