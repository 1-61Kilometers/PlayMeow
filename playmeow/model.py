import numpy as np
import tensorflow as tf
from tensorflow.keras.models import Sequential, load_model
from tensorflow.keras.layers import Dense, Input, Dropout
from tensorflow.keras.optimizers import Adam
from tensorflow.keras.callbacks import EarlyStopping, ReduceLROnPlateau
from tensorflow.keras.models import save_model
from sklearn.model_selection import KFold

class PlayMeowModel:
    def __init__(self, input_shape):
        """
        Initialize the PlayMeow neural network model.
        
        Args:
            input_shape: Shape of input data (window_size, n_features)
        """
        self.input_shape = input_shape
        self.model = self._build_model()
        
    def _build_model(self):
        """
        Build the neural network architecture.
        
        Returns:
            Keras model
        """
        model = Sequential([
            # Flatten the input (window_size, n_features)
            Input(shape=self.input_shape),
            tf.keras.layers.Flatten(),
            
            # Hidden layers
            Dense(128, activation='relu'),
            Dropout(0.2),
            Dense(256, activation='relu'),
            Dropout(0.2),
            Dense(128, activation='relu'),
            Dropout(0.2),
            
            # Output layer: 2 neurons for X and Z movement vectors
            Dense(2, activation='tanh')  # tanh for [-1, 1] range
        ])
        
        # Compile model
        model.compile(
            optimizer=Adam(learning_rate=1e-3),
            loss='mse'
        )
        
        return model
    
    def train_supervised(self, X_train, y_train, X_val=None, y_val=None, 
                         batch_size=32, epochs=100, cross_val=True, n_folds=5):
        """
        Train the model using supervised learning (behavioral cloning).
        
        Args:
            X_train: Training features
            y_train: Training targets
            X_val: Validation features
            y_val: Validation targets
            batch_size: Batch size for training
            epochs: Maximum number of epochs
            cross_val: Whether to use cross-validation
            n_folds: Number of cross-validation folds
            
        Returns:
            Training history
        """
        callbacks = [
            EarlyStopping(monitor='val_loss', patience=10, restore_best_weights=True),
            ReduceLROnPlateau(monitor='val_loss', factor=0.5, patience=5, min_lr=1e-6)
        ]
        
        if cross_val and X_val is None:
            # Perform k-fold cross-validation
            kfold = KFold(n_splits=n_folds, shuffle=True, random_state=42)
            fold_histories = []
            
            for fold, (train_idx, val_idx) in enumerate(kfold.split(X_train)):
                print(f"Training fold {fold+1}/{n_folds}")
                
                # Get fold training and validation data
                X_fold_train, X_fold_val = X_train[train_idx], X_train[val_idx]
                y_fold_train, y_fold_val = y_train[train_idx], y_train[val_idx]
                
                # Reset model for each fold
                self.model = self._build_model()
                
                # Train fold
                history = self.model.fit(
                    X_fold_train, y_fold_train,
                    validation_data=(X_fold_val, y_fold_val),
                    batch_size=batch_size,
                    epochs=epochs,
                    callbacks=callbacks,
                    verbose=1
                )
                
                fold_histories.append(history)
            
            return fold_histories
        else:
            # Train with provided validation set
            history = self.model.fit(
                X_train, y_train,
                validation_data=(X_val, y_val) if X_val is not None else None,
                batch_size=batch_size,
                epochs=epochs,
                callbacks=callbacks,
                validation_split=0.2 if X_val is None else 0.0,
                verbose=1
            )
            
            return history
    
    def predict(self, X):
        """
        Generate movement predictions.
        
        Args:
            X: Input features
            
        Returns:
            Predicted movement vectors
        """
        return self.model.predict(X)
    
    def save(self, filepath):
        """
        Save the trained model.
        
        Args:
            filepath: Path to save the model
        """
        save_model(self.model, filepath)
        
    def load(self, filepath):
        """
        Load a trained model.
        
        Args:
            filepath: Path to the saved model
        """
        # Add custom objects dict to resolve 'mse' not found error
        from tensorflow.keras.losses import MeanSquaredError
        self.model = load_model(filepath, custom_objects={'mse': MeanSquaredError()})