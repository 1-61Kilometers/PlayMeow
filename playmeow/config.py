"""
Configuration module for the PlayMeow system.

This module centralizes configuration settings for all components
of the PlayMeow system, making it easier to modify parameters
without changing the code.
"""

import os
import json
import logging
from pathlib import Path

# Set up logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler('playmeow.log')
    ]
)
logger = logging.getLogger(__name__)

# Default config values
_DEFAULT_CONFIG = {
    # Common settings
    "common": {
        "default_model_path": "playmeow/models/playmeow_model.h5",
        "default_data_path": "playmeow/data/sample_data.csv",
        "window_size": 10,
        "prediction_horizon": 5,
        "input_shape": [10, 10]
    },

    # Data processing settings
    "data_processor": {
        "test_split": 0.2
    },

    # Neural network model settings
    "model": {
        "learning_rate": 1e-3,
        "hidden_layer_sizes": [128, 256, 128],
        "dropout_rate": 0.2
    },

    # Training settings
    "training": {
        "batch_size": 32,
        "max_epochs": 100,
        "patience": 10,
        "reduce_lr_factor": 0.5,
        "reduce_lr_patience": 5,
        "min_lr": 1e-6
    },

    # Reinforcement learning settings
    "reinforcement": {
        "gamma": 0.99,
        "replay_buffer_size": 10000,
        "initial_epsilon": 1.0,
        "final_epsilon": 0.1,
        "epsilon_decay_steps": 500,
        "target_update_tau": 0.1
    },

    # Simulation settings
    "simulation": {
        "bounds": [-2, 2, -2, 2],
        "cat_speed_range": [0.1, 0.8],
        "engagement_threshold": 0.3,
        "max_episode_steps": 300
    },

    # Unity integration settings
    "unity_integration": {
        "host": "localhost",
        "port": 12345,
        "prediction_rate": 0.1  # seconds (10 Hz)
    },

    # Web application settings
    "webapp": {
        "host": "0.0.0.0",
        "port": 5000,
        "debug": False,
        "secret_key": "playmeow_flask_secret_key"  # Should be overridden in production
    }
}

# Global config object
_config = None

def load_config(config_path=None):
    """
    Load configuration from a JSON file.

    Args:
        config_path: Path to the config file (optional)

    Returns:
        dict: Configuration dictionary
    """
    global _config

    # Start with default config
    _config = _DEFAULT_CONFIG.copy()

    # If config path is provided, load and merge with defaults
    if config_path and os.path.exists(config_path):
        try:
            with open(config_path, 'r') as f:
                user_config = json.load(f)

            # Merge user config with defaults (deep merge)
            _merge_configs(_config, user_config)
            logger.info(f"Loaded configuration from {config_path}")

        except Exception as e:
            logger.error(f"Error loading config from {config_path}: {e}")
            logger.info("Using default configuration")
    else:
        logger.info("No config file specified or found. Using default configuration.")

    return _config

def _merge_configs(default_config, user_config):
    """
    Deep merge of user config into default config.

    Args:
        default_config: Default configuration dictionary
        user_config: User configuration dictionary to merge in
    """
    for key, value in user_config.items():
        if key in default_config:
            if isinstance(value, dict) and isinstance(default_config[key], dict):
                _merge_configs(default_config[key], value)
            else:
                default_config[key] = value
        else:
            default_config[key] = value

def get_config():
    """
    Get the current configuration.

    Returns:
        dict: Configuration dictionary
    """
    global _config
    if _config is None:
        load_config()
    return _config

def save_config(config_path):
    """
    Save the current configuration to a file.

    Args:
        config_path: Path to save the config

    Returns:
        bool: True if successful, False otherwise
    """
    global _config
    if _config is None:
        load_config()

    try:
        # Ensure directory exists
        Path(config_path).parent.mkdir(parents=True, exist_ok=True)

        with open(config_path, 'w') as f:
            json.dump(_config, f, indent=4)

        logger.info(f"Configuration saved to {config_path}")
        return True
    except Exception as e:
        logger.error(f"Error saving config to {config_path}: {e}")
        return False

def update_config(section, key, value):
    """
    Update a specific configuration value.

    Args:
        section: Config section name
        key: Config key
        value: New value

    Returns:
        bool: True if successful, False otherwise
    """
    global _config
    if _config is None:
        load_config()

    try:
        if section in _config:
            _config[section][key] = value
            logger.info(f"Updated config: {section}.{key} = {value}")
            return True
        else:
            logger.error(f"Config section '{section}' not found")
            return False
    except Exception as e:
        logger.error(f"Error updating config: {e}")
        return False

# Initialize config