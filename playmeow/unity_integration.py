import numpy as np
import json
import csv
import time
import socket
import threading
from pathlib import Path

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
        """
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.bind((self.host, self.port))
        self.socket.listen(1)
        
        self.running = True
        self.server_thread = threading.Thread(target=self._run_server)
        self.server_thread.start()
        
        print(f"Unity integration server started on {self.host}:{self.port}")
        
    def stop(self):
        """
        Stop the integration server.
        """
        self.running = False
        if self.socket:
            self.socket.close()
        
        if hasattr(self, 'server_thread') and self.server_thread.is_alive():
            self.server_thread.join()
        
        print("Unity integration server stopped")
        
    def _run_server(self):
        """
        Run the server loop to handle Unity connections.
        """
        while self.running:
            try:
                client_socket, address = self.socket.accept()
                print(f"Connection from {address}")
                
                client_handler = threading.Thread(
                    target=self._handle_client,
                    args=(client_socket,)
                )
                client_handler.start()
                
            except socket.error:
                if self.running:
                    print("Socket error occurred")
                break
    
    def _handle_client(self, client_socket):
        """
        Handle a client connection.
        
        Args:
            client_socket: Socket for the client connection
        """
        try:
            while self.running:
                # Receive data from Unity
                data = client_socket.recv(1024)
                if not data:
                    break
                
                # Parse JSON data
                try:
                    json_data = json.loads(data.decode('utf-8'))
                    response = self._process_data(json_data)
                    
                    # Send response back to Unity
                    client_socket.send(json.dumps(response).encode('utf-8'))
                    
                except json.JSONDecodeError:
                    print("Error decoding JSON data")
                
        except socket.error:
            print("Client socket error")
        finally:
            client_socket.close()
    
    def _process_data(self, data):
        """
        Process data from Unity and generate a response.
        
        Args:
            data: JSON data from Unity
            
        Returns:
            Response dictionary
        """
        try:
            # Extract data
            cat_pos = np.array([data['catX'], data['catZ']])
            laser_pos = np.array([data['laserX'], data['laserZ']])
            engagement = data.get('engagement', False)
            timestamp = data.get('timestamp', time.time())
            
            # Store positions
            self.last_cat_pos = cat_pos
            self.last_laser_pos = laser_pos
            
            # Add to data buffer for possible recording
            with self.buffer_lock:
                self.data_buffer.append({
                    'timestamp': timestamp,
                    'cat_x': cat_pos[0],
                    'cat_y': data.get('catY', 0),
                    'cat_z': cat_pos[1],
                    'laser_x': laser_pos[0],
                    'laser_y': data.get('laserY', 0),
                    'laser_z': laser_pos[1],
                    'engagement': 1 if engagement else 0
                })
            
            # Check if we should make a prediction
            current_time = time.time()
            if current_time - self.last_prediction_time >= 0.1:  # 10 Hz prediction rate
                self.last_prediction_time = current_time
                
                # Prepare input features
                cat_rel_pos = cat_pos - laser_pos
                
                # Calculate cat velocity (if we have previous positions)
                cat_velocity = np.zeros(2)
                if hasattr(self, 'prev_cat_pos') and hasattr(self, 'prev_timestamp'):
                    time_diff = timestamp - self.prev_timestamp
                    if time_diff > 0:
                        cat_velocity = (cat_pos - self.prev_cat_pos) / time_diff
                
                # Save current position and timestamp for next velocity calculation
                self.prev_cat_pos = cat_pos
                self.prev_timestamp = timestamp
                
                # Calculate cat speed and direction
                cat_speed = np.linalg.norm(cat_velocity)
                cat_direction = 0
                if cat_speed > 0:
                    cat_direction = np.arctan2(cat_velocity[1], cat_velocity[0])
                
                # Calculate distances to boundaries
                bounds = data.get('bounds', [-2, 2, -2, 2])
                distance_to_boundary_x = min(
                    laser_pos[0] - bounds[0],
                    bounds[1] - laser_pos[0]
                )
                distance_to_boundary_z = min(
                    laser_pos[1] - bounds[2],
                    bounds[3] - laser_pos[1]
                )
                
                # Determine time since engagement
                time_since_engagement = data.get('timeSinceEngagement', 0)
                
                # Construct input features
                features = np.array([
                    cat_rel_pos[0],  # Cat's X relative to laser
                    cat_rel_pos[1],  # Cat's Z relative to laser
                    cat_velocity[0],  # Cat's X velocity
                    cat_velocity[1],  # Cat's Z velocity
                    cat_speed,  # Cat's speed
                    cat_direction,  # Cat's direction angle
                    float(engagement),  # Engagement indicator
                    time_since_engagement / 100.0,  # Normalized time since engagement
                    distance_to_boundary_x,  # Distance to X boundary
                    distance_to_boundary_z   # Distance to Z boundary
                ])
                
                # Reshape to match expected input shape (window_size, n_features)
                observation = np.tile(features, (10, 1))
                
                # Make prediction
                movement_vector = self.model.predict(np.array([observation]))[0]
                
                # Return movement vector as response
                return {
                    'status': 'success',
                    'moveX': float(movement_vector[0]),
                    'moveZ': float(movement_vector[1])
                }
            
            # If not making a prediction, just acknowledge
            return {'status': 'received'}
            
        except Exception as e:
            print(f"Error processing data: {e}")
            return {'status': 'error', 'message': str(e)}
    
    def save_recorded_data(self, filepath):
        """
        Save recorded data to a CSV file.
        
        Args:
            filepath: Path to save the CSV file
        """
        with self.buffer_lock:
            if not self.data_buffer:
                print("No data to save")
                return
                
            # Ensure directory exists
            Path(filepath).parent.mkdir(parents=True, exist_ok=True)
            
            # Write to CSV
            with open(filepath, 'w', newline='') as csvfile:
                fieldnames = self.data_buffer[0].keys()
                writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
                
                writer.writeheader()
                for row in self.data_buffer:
                    writer.writerow(row)
            
            print(f"Saved {len(self.data_buffer)} records to {filepath}")
            
            # Clear buffer after saving
            self.data_buffer = []
    
    def clear_data_buffer(self):
        """
        Clear the data buffer.
        """
        with self.buffer_lock:
            self.data_buffer = []
            print("Data buffer cleared")