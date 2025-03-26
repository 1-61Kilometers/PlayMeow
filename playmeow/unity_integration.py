import numpy as np
import json
import csv
import time
import socket
import threading
import logging
from pathlib import Path

# Configure logging
logger = logging.getLogger(__name__)

class UnityIntegration:
    def __init__(self, model, host='localhost', port=12345):
        """
        Integration with Unity for the PlayMeow system.
        
        Args:
            model: Trained PlayMeow model
            host: Host for socket connection
            port: Port for socket connection
        """
        self.model = model
        self.host = host
        self.port = port
        self.socket = None
        self.running = False
        self.last_cat_pos = None
        self.last_laser_pos = None
        self.last_prediction_time = 0
        self.data_buffer = []
        self.buffer_lock = threading.Lock()
        
    def start(self):
        """
        Start the integration server.
        
        Raises:
            socket.error: If there's an issue binding to the port
        """
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            # Set socket option to reuse address
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind((self.host, self.port))
            self.socket.listen(1)
            
            self.running = True
            self.server_thread = threading.Thread(target=self._run_server)
            self.server_thread.daemon = True  # Make thread a daemon so it exits when main thread exits
            self.server_thread.start()
            
            logger.info(f"Unity integration server started on {self.host}:{self.port}")
        except socket.error as e:
            logger.error(f"Failed to start Unity integration server: {e}")
            raise
        
    def stop(self):
        """
        Stop the integration server.
        """
        try:
            self.running = False
            if self.socket:
                self.socket.close()
            
            if hasattr(self, 'server_thread') and self.server_thread.is_alive():
                # Set a timeout for joining the thread to prevent hanging
                self.server_thread.join(timeout=5.0)
                if self.server_thread.is_alive():
                    logger.warning("Server thread did not terminate gracefully within timeout")
            
            logger.info("Unity integration server stopped")
        except Exception as e:
            logger.error(f"Error stopping Unity integration server: {e}")
        
    def _run_server(self):
        """
        Run the server loop to handle Unity connections.
        """
        while self.running:
            try:
                client_socket, address = self.socket.accept()
                logger.info(f"Connection from {address}")
                
                client_handler = threading.Thread(
                    target=self._handle_client,
                    args=(client_socket,)
                )
                client_handler.daemon = True  # Make thread a daemon
                client_handler.start()
                
            except socket.error as e:
                if self.running:
                    logger.error(f"Socket error occurred: {e}")
                break
            except Exception as e:
                logger.error(f"Unexpected error in server loop: {e}")
                if self.running:
                    # Sleep briefly to avoid CPU spinning in case of persistent errors
                    time.sleep(1)
    
    def _handle_client(self, client_socket):
        """
        Handle a client connection.
        
        Args:
            client_socket: Socket for the client connection
        """
        client_address = client_socket.getpeername() if hasattr(client_socket, 'getpeername') else "Unknown"
        logger.info(f"Starting client handler for {client_address}")
        
        try:
            # Set a timeout to prevent hanging indefinitely
            client_socket.settimeout(60.0)  # 60 second timeout
            
            while self.running:
                try:
                    # Receive data from Unity
                    data = client_socket.recv(1024)
                    if not data:
                        logger.info(f"Client {client_address} disconnected")
                        break
                    
                    # Parse JSON data
                    try:
                        json_data = json.loads(data.decode('utf-8'))
                        response = self._process_data(json_data)
                        
                        # Send response back to Unity
                        client_socket.send(json.dumps(response).encode('utf-8'))
                        
                    except json.JSONDecodeError as e:
                        logger.error(f"Error decoding JSON data from {client_address}: {e}")
                        # Send error response
                        error_response = {'status': 'error', 'message': 'Invalid JSON format'}
                        client_socket.send(json.dumps(error_response).encode('utf-8'))
                        
                except socket.timeout:
                    logger.warning(f"Socket timeout for {client_address}")
                    # Send a ping to check if client is still connected
                    try:
                        client_socket.send(json.dumps({'status': 'ping'}).encode('utf-8'))
                    except socket.error:
                        logger.info(f"Client {client_address} disconnected during ping")
                        break
                        
        except socket.error as e:
            logger.error(f"Socket error for client {client_address}: {e}")
        except Exception as e:
            logger.error(f"Unexpected error handling client {client_address}: {e}")
        finally:
            try:
                client_socket.close()
                logger.info(f"Closed connection to {client_address}")
            except Exception as e:
                logger.error(f"Error closing client socket: {e}")
    
    def _process_data(self, data):
        """
        Process data from Unity and generate a response.
        
        Args:
            data: JSON data from Unity
            
        Returns:
            Response dictionary
        """
        try:
            # Validate required fields
            required_fields = ['catX', 'catZ', 'laserX', 'laserZ']
            for field in required_fields:
                if field not in data:
                    logger.error(f"Missing required field in data: {field}")
                    return {'status': 'error', 'message': f"Missing required field: {field}"}
            
            # Extract data
            cat_pos = np.array([data['catX'], data['catZ']], dtype=np.float32)
            laser_pos = np.array([data['laserX'], data['laserZ']], dtype=np.float32)
            engagement = data.get('engagement', False)
            timestamp = data.get('timestamp', time.time())
            
            # Store positions
            self.last_cat_pos = cat_pos
            self.last_laser_pos = laser_pos
            
            # Add to data buffer for possible recording
            with self.buffer_lock:
                self.data_buffer.append({
                    'timestamp': timestamp,
                    'cat_x': float(cat_pos[0]),
                    'cat_y': float(data.get('catY', 0)),
                    'cat_z': float(cat_pos[1]),
                    'laser_x': float(laser_pos[0]),
                    'laser_y': float(data.get('laserY', 0)),
                    'laser_z': float(laser_pos[1]),
                    'engagement': 1 if engagement else 0
                })
            
            # Check if we should make a prediction
            current_time = time.time()
            if current_time - self.last_prediction_time >= 0.1:  # 10 Hz prediction rate
                self.last_prediction_time = current_time
                
                # Prepare input features
                cat_rel_pos = cat_pos - laser_pos
                
                # Calculate cat velocity (if we have previous positions)
                cat_velocity = np.zeros(2, dtype=np.float32)
                if hasattr(self, 'prev_cat_pos') and hasattr(self, 'prev_timestamp'):
                    time_diff = timestamp - self.prev_timestamp
                    if time_diff > 0:
                        cat_velocity = (cat_pos - self.prev_cat_pos) / time_diff
                
                # Save current position and timestamp for next velocity calculation
                self.prev_cat_pos = cat_pos.copy()  # Create a copy to prevent reference issues
                self.prev_timestamp = timestamp
                
                # Calculate cat speed and direction
                cat_speed = np.linalg.norm(cat_velocity)
                cat_direction = 0
                if cat_speed > 0:
                    cat_direction = np.arctan2(cat_velocity[1], cat_velocity[0])
                
                # Calculate distances to boundaries with validation
                bounds = data.get('bounds', [-2, 2, -2, 2])
                if len(bounds) != 4:
                    logger.warning(f"Invalid bounds format: {bounds}, using default [-2, 2, -2, 2]")
                    bounds = [-2, 2, -2, 2]
                    
                distance_to_boundary_x = max(0, min(
                    laser_pos[0] - bounds[0],
                    bounds[1] - laser_pos[0]
                ))
                distance_to_boundary_z = max(0, min(
                    laser_pos[1] - bounds[2],
                    bounds[3] - laser_pos[1]
                ))
                
                # Determine time since engagement with validation
                time_since_engagement = max(0, data.get('timeSinceEngagement', 0))
                
                # Construct input features
                features = np.array([
                    cat_rel_pos[0],  # Cat's X relative to laser
                    cat_rel_pos[1],  # Cat's Z relative to laser
                    cat_velocity[0],  # Cat's X velocity
                    cat_velocity[1],  # Cat's Z velocity
                    cat_speed,  # Cat's speed
                    cat_direction,  # Cat's direction angle
                    float(engagement),  # Engagement indicator
                    min(1.0, time_since_engagement / 100.0),  # Normalized time since engagement (capped at 1.0)
                    distance_to_boundary_x,  # Distance to X boundary
                    distance_to_boundary_z   # Distance to Z boundary
                ], dtype=np.float32)
                
                # Check for NaN values
                if np.isnan(features).any():
                    logger.warning("NaN values detected in features, replacing with zeros")
                    features = np.nan_to_num(features)
                
                # Reshape to match expected input shape (window_size, n_features)
                observation = np.tile(features, (10, 1))
                
                try:
                    # Make prediction with error handling
                    movement_vector = self.model.predict(np.array([observation]))[0]
                    
                    # Validate prediction output
                    if np.isnan(movement_vector).any():
                        logger.error("Model produced NaN predictions")
                        movement_vector = np.array([0.0, 0.0])
                    
                    # Return movement vector as response
                    return {
                        'status': 'success',
                        'moveX': float(movement_vector[0]),
                        'moveZ': float(movement_vector[1])
                    }
                except Exception as model_error:
                    logger.error(f"Error making prediction: {model_error}")
                    return {'status': 'error', 'message': f"Prediction error: {str(model_error)}"}
            
            # If not making a prediction, just acknowledge
            return {'status': 'received'}
            
        except KeyError as e:
            logger.error(f"Missing key in data: {e}")
            return {'status': 'error', 'message': f"Missing data field: {str(e)}"}
        except ValueError as e:
            logger.error(f"Value error processing data: {e}")
            return {'status': 'error', 'message': f"Value error: {str(e)}"}
        except Exception as e:
            logger.error(f"Error processing data: {e}")
            return {'status': 'error', 'message': f"Processing error: {str(e)}"}
    
    def save_recorded_data(self, filepath):
        """
        Save recorded data to a CSV file.
        
        Args:
            filepath: Path to save the CSV file
            
        Returns:
            bool: True if save was successful, False otherwise
        """
        with self.buffer_lock:
            if not self.data_buffer:
                logger.warning("No data to save")
                return False
            
            try:
                # Ensure directory exists
                Path(filepath).parent.mkdir(parents=True, exist_ok=True)
                
                # Write to CSV
                with open(filepath, 'w', newline='') as csvfile:
                    if not self.data_buffer:
                        logger.warning("Data buffer empty, no data to save")
                        return False
                        
                    fieldnames = self.data_buffer[0].keys()
                    writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
                    
                    writer.writeheader()
                    records_saved = 0
                    
                    for row in self.data_buffer:
                        try:
                            writer.writerow(row)
                            records_saved += 1
                        except Exception as row_error:
                            logger.error(f"Error writing row to CSV: {row_error}")
                
                logger.info(f"Saved {records_saved} records to {filepath}")
                
                # Clear buffer after saving
                self.data_buffer = []
                return True
                
            except IOError as e:
                logger.error(f"IO error saving data to {filepath}: {e}")
                return False
            except Exception as e:
                logger.error(f"Unexpected error saving data: {e}")
                return False
    
    def clear_data_buffer(self):
        """
        Clear the data buffer.
        
        Returns:
            int: Number of records cleared
        """
        with self.buffer_lock:
            num_records = len(self.data_buffer)
            self.data_buffer = []
            logger.info(f"Data buffer cleared ({num_records} records)")
            return num_records