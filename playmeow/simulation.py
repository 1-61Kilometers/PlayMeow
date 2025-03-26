import numpy as np
import pandas as pd
import time

class PlayMeowSimulation:
    def __init__(self, data=None, bounds=(-2, 2, -2, 2), 
                cat_speed_range=(0.1, 0.8), engagement_threshold=0.3):
        """
        Simulation environment for PlayMeow training.
        
        Args:
            data: Optional DataFrame with recorded data to use for simulation
            bounds: (min_x, max_x, min_z, max_z) bounds for the play area
            cat_speed_range: (min_speed, max_speed) for simulated cat
            engagement_threshold: Distance threshold for cat engagement
        """
        self.data = data
        self.bounds = bounds
        self.cat_speed_range = cat_speed_range
        self.engagement_threshold = engagement_threshold
        
        # Current state
        self.cat_pos = np.zeros(2)  # (x, z)
        self.laser_pos = np.zeros(2)  # (x, z)
        self.cat_velocity = np.zeros(2)  # (vx, vz)
        self.engagement = False
        self.time_since_engagement = 0
        self.play_duration = 0
        self.cat_interest_level = 1.0  # 0.0 to 1.0, decreases over time without engagement
        self.episode_steps = 0
        self.max_episode_steps = 300
        
    
    def reset(self):
        """
        Reset the simulation for a new episode.
        
        Returns:
            Initial state observation
        """
        # Randomize positions
        self.cat_pos = np.array([
            np.random.uniform(self.bounds[0], self.bounds[1]),
            np.random.uniform(self.bounds[2], self.bounds[3])
        ])
        
        # Initial laser position slightly away from cat
        angle = np.random.uniform(0, 2 * np.pi)
        distance = np.random.uniform(0.5, 1.0)
        self.laser_pos = self.cat_pos + np.array([
            distance * np.cos(angle),
            distance * np.sin(angle)
        ])
        
        # Constrain laser position to bounds
        self.laser_pos[0] = np.clip(self.laser_pos[0], self.bounds[0], self.bounds[1])
        self.laser_pos[1] = np.clip(self.laser_pos[1], self.bounds[2], self.bounds[3])
        
        # Reset other state variables
        self.cat_velocity = np.zeros(2)
        self.engagement = False
        self.time_since_engagement = 0
        self.play_duration = 0
        self.cat_interest_level = 1.0
        self.episode_steps = 0
        
        return self._get_observation()
    
    def step(self, action):
        """
        Take a step in the simulation.
        
        Args:
            action: Movement vector for laser (dx, dz)
            
        Returns:
            (next_state, reward, done, info)
        """
        self.episode_steps += 1
        
        # Move laser based on action
        new_laser_pos = self.laser_pos + (action * 0.1)  # Scale action to reasonable movement
        
        # Constrain laser position to bounds
        new_laser_pos[0] = np.clip(new_laser_pos[0], self.bounds[0], self.bounds[1])
        new_laser_pos[1] = np.clip(new_laser_pos[1], self.bounds[2], self.bounds[3])
        
        
        # Update laser position
        self.laser_pos = new_laser_pos
        
        # Update cat behavior
        self._update_cat_behavior()
        
        # Check engagement
        distance_to_cat = np.linalg.norm(self.cat_pos - self.laser_pos)
        prev_engagement = self.engagement
        self.engagement = (distance_to_cat < self.engagement_threshold) and (self.cat_interest_level > 0.3)
        
        # Update time since engagement
        if self.engagement:
            self.time_since_engagement = 0
            # Increase interest if engaged
            self.cat_interest_level = min(1.0, self.cat_interest_level + 0.05)
        else:
            self.time_since_engagement += 1
            # Decrease interest if not engaged
            self.cat_interest_level = max(0.0, self.cat_interest_level - 0.01)
        
        # Increment play duration
        self.play_duration += 1
        
        # Calculate reward
        in_prohibited_zone = False
        session_complete = self.play_duration >= 100 and self.cat_interest_level > 0.6
        session_abandoned = self.cat_interest_level < 0.2
        
        # Calculate distances to boundaries
        distance_to_boundary_x = min(
            self.laser_pos[0] - self.bounds[0],
            self.bounds[1] - self.laser_pos[0]
        )
        distance_to_boundary_z = min(
            self.laser_pos[1] - self.bounds[2],
            self.bounds[3] - self.laser_pos[1]
        )
        
        # Calculate reward
        reward = 0
        
        # Engagement rewards
        if self.engagement:
            reward += 0.1  # High engagement
        else:
            reward -= 0.2  # Low engagement
        
        # Session completion rewards
        if session_complete:
            reward += 0.4
        elif session_abandoned:
            reward -= 0.3
        
        # Distance reward (appropriate distance maintenance)
        if 0.2 <= distance_to_cat <= 1.0:
            reward += 0.01
        
        # Distance from boundary reward
        boundary_margin = 0.25
        min_dist_to_boundary = min(
            self.cat_pos[0] - self.bounds[0],
            self.bounds[1] - self.cat_pos[0],
            self.cat_pos[1] - self.bounds[2],
            self.bounds[3] - self.cat_pos[1]
        )
        
        # Add reward for keeping cat away from boundaries
        if min_dist_to_boundary < boundary_margin:
            # Cat is near boundary - negative reward
            boundary_penalty = 0.05 * (1.0 - min_dist_to_boundary/boundary_margin)
            reward -= boundary_penalty
        
        # Add reward for moving away from corners
        corner_distance = 0.35  # Distance to detect near corner
        corners = [
            (self.bounds[0], self.bounds[2]),  # Bottom-left
            (self.bounds[0], self.bounds[3]),  # Top-left
            (self.bounds[1], self.bounds[2]),  # Bottom-right
            (self.bounds[1], self.bounds[3])   # Top-right
        ]
        
        # Check distance to each corner
        for corner in corners:
            corner_dist = np.linalg.norm(self.cat_pos - corner)
            if corner_dist < corner_distance:
                # Cat is near corner - higher penalty
                corner_penalty = 0.1 * (1.0 - corner_dist/corner_distance)
                reward -= corner_penalty
                break  # Only penalize once for being in any corner
        
        # Prohibited zone penalty (kept for backward compatibility)
        if in_prohibited_zone:
            reward -= 0.1
        
        # Check if episode is done
        done = (
            session_complete or 
            session_abandoned or
            self.episode_steps >= self.max_episode_steps
        )
        
        # Get next state observation
        next_state = self._get_observation()
        
        # Additional info
        info = {
            'distance_to_cat': distance_to_cat,
            'engagement': self.engagement,
            'cat_interest_level': self.cat_interest_level,
            'in_prohibited_zone': in_prohibited_zone,
            'session_complete': session_complete,
            'session_abandoned': session_abandoned
        }
        
        return next_state, reward, done, info
    
    def _update_cat_behavior(self):
        """
        Update simulated cat behavior with improved boundary handling.
        """
        # Calculate vector from cat to laser
        vector_to_laser = self.laser_pos - self.cat_pos
        distance_to_laser = np.linalg.norm(vector_to_laser)
        
        if distance_to_laser > 0:
            direction_to_laser = vector_to_laser / distance_to_laser
        else:
            direction_to_laser = np.array([0, 0])
        
        # Check if cat is near boundary
        boundary_margin = 0.25  # Distance at which to consider the cat near a boundary
        near_boundary_x = (self.cat_pos[0] < self.bounds[0] + boundary_margin or 
                         self.cat_pos[0] > self.bounds[1] - boundary_margin)
        near_boundary_z = (self.cat_pos[1] < self.bounds[2] + boundary_margin or 
                         self.cat_pos[1] > self.bounds[3] - boundary_margin)
        near_boundary = near_boundary_x or near_boundary_z
        
        # Check if cat is in a corner
        in_corner = near_boundary_x and near_boundary_z
        
        # Determine cat's interest and direction
        if self.cat_interest_level > 0.3 and distance_to_laser < 1.5:
            # Cat is interested and sees the laser
            if distance_to_laser < 0.1:
                # Cat "caught" the laser, move randomly for a bit
                angle = np.random.uniform(0, 2 * np.pi)
                self.cat_velocity = np.array([
                    np.cos(angle),
                    np.sin(angle)
                ]) * np.random.uniform(*self.cat_speed_range)
            else:
                # Calculate repulsion from boundaries to avoid getting stuck
                boundary_repulsion = np.zeros(2)
                if near_boundary:
                    # Add repulsion force from nearby boundaries
                    dist_to_left = self.cat_pos[0] - self.bounds[0]
                    dist_to_right = self.bounds[1] - self.cat_pos[0]
                    dist_to_bottom = self.cat_pos[1] - self.bounds[2]
                    dist_to_top = self.bounds[3] - self.cat_pos[1]
                    
                    # X-axis repulsion (stronger when very close)
                    if dist_to_left < boundary_margin:
                        boundary_repulsion[0] += 1.0 * (1.0 - dist_to_left/boundary_margin)
                    if dist_to_right < boundary_margin:
                        boundary_repulsion[0] -= 1.0 * (1.0 - dist_to_right/boundary_margin)
                        
                    # Z-axis repulsion (stronger when very close)
                    if dist_to_bottom < boundary_margin:
                        boundary_repulsion[1] += 1.0 * (1.0 - dist_to_bottom/boundary_margin)
                    if dist_to_top < boundary_margin:
                        boundary_repulsion[1] -= 1.0 * (1.0 - dist_to_top/boundary_margin)
                
                # Move toward laser with randomness
                random_factor = np.random.uniform(0.7, 1.0) if self.cat_interest_level > 0.7 else np.random.uniform(0.3, 0.7)
                noise = np.random.normal(0, 0.3, 2)
                
                # Combine direction to laser with noise and boundary repulsion
                if in_corner:
                    # In corners, prioritize getting away from boundaries
                    repulsion_weight = 1.5
                    direction_weight = 0.5
                    noise_weight = 0.5
                elif near_boundary:
                    # Near boundaries, balance direction and repulsion
                    repulsion_weight = 1.0
                    direction_weight = 0.7
                    noise_weight = 0.3
                else:
                    # Away from boundaries, focus on laser
                    repulsion_weight = 0.0
                    direction_weight = random_factor
                    noise_weight = 1 - random_factor
                
                # Normalize repulsion if non-zero
                if np.linalg.norm(boundary_repulsion) > 0:
                    boundary_repulsion = boundary_repulsion / np.linalg.norm(boundary_repulsion)
                
                # Combine forces
                direction = (direction_to_laser * direction_weight + 
                            noise * noise_weight + 
                            boundary_repulsion * repulsion_weight)
                
                # Normalize the combined direction vector
                if np.linalg.norm(direction) > 0:
                    direction = direction / np.linalg.norm(direction)
                
                # Set velocity based on interest level and position
                base_speed = np.random.uniform(*self.cat_speed_range)
                
                # Cats move faster when away from boundaries, slower near boundaries
                position_factor = 1.0
                if near_boundary:
                    position_factor = 0.8
                if in_corner:
                    position_factor = 0.7
                
                speed = base_speed * self.cat_interest_level * position_factor
                self.cat_velocity = direction * speed
        else:
            # Cat is not interested, move randomly or slow down
            if np.random.random() < 0.1:
                # Random movement with boundary awareness
                if near_boundary:
                    # If near boundary, bias movement away from it
                    center = np.array([(self.bounds[0] + self.bounds[1]) / 2, 
                                    (self.bounds[2] + self.bounds[3]) / 2])
                    to_center = center - self.cat_pos
                    angle = np.arctan2(to_center[1], to_center[0]) + np.random.normal(0, np.pi/4)
                else:
                    # Normal random movement
                    angle = np.random.uniform(0, 2 * np.pi)
                
                self.cat_velocity = np.array([
                    np.cos(angle),
                    np.sin(angle)
                ]) * np.random.uniform(0, self.cat_speed_range[0])
            else:
                # Slow down
                self.cat_velocity *= 0.9
        
        # Add a small chance to make a sudden movement (cats do this!)
        if np.random.random() < 0.01 and not near_boundary:  # 1% chance, but not near boundaries
            random_impulse = np.random.normal(0, 0.5, 2)
            self.cat_velocity += random_impulse
        
        # Update cat position
        new_cat_pos = self.cat_pos + self.cat_velocity
        
        # Constrain cat position to bounds with bounce and improved handling
        for i in range(2):
            if new_cat_pos[i] < self.bounds[i*2]:
                new_cat_pos[i] = self.bounds[i*2] + 0.01  # Small offset to prevent sticking
                self.cat_velocity[i] = abs(self.cat_velocity[i]) * 0.5  # Bounce and slow down
            elif new_cat_pos[i] > self.bounds[i*2+1]:
                new_cat_pos[i] = self.bounds[i*2+1] - 0.01  # Small offset to prevent sticking
                self.cat_velocity[i] = -abs(self.cat_velocity[i]) * 0.5  # Bounce and slow down
        
        self.cat_pos = new_cat_pos
    
    def _get_observation(self):
        """
        Get the current state observation.
        
        Returns:
            Observation array
        """
        # Calculate cat's position relative to laser
        cat_rel_pos = self.cat_pos - self.laser_pos
        
        # Calculate distances to boundaries
        distance_to_boundary_x = min(
            self.laser_pos[0] - self.bounds[0],
            self.bounds[1] - self.laser_pos[0]
        )
        distance_to_boundary_z = min(
            self.laser_pos[1] - self.bounds[2],
            self.bounds[3] - self.laser_pos[1]
        )
        
        # Normalize cat velocity to get direction
        cat_speed = np.linalg.norm(self.cat_velocity)
        if cat_speed > 0:
            cat_direction = self.cat_velocity / cat_speed
        else:
            cat_direction = np.array([0, 0])
        
        # Combine all features
        features = np.array([
            cat_rel_pos[0],  # Cat's X relative to laser
            cat_rel_pos[1],  # Cat's Z relative to laser
            self.cat_velocity[0],  # Cat's X velocity
            self.cat_velocity[1],  # Cat's Z velocity
            cat_speed,  # Cat's speed
            np.arctan2(cat_direction[1], cat_direction[0]),  # Cat's direction angle
            float(self.engagement),  # Engagement indicator
            self.time_since_engagement / 100.0,  # Normalized time since engagement
            distance_to_boundary_x,  # Distance to X boundary
            distance_to_boundary_z   # Distance to Z boundary
        ])
        
        # Reshape to match expected input shape (window_size, n_features)
        # For simplicity, we're repeating the current observation for all timesteps
        observation = np.tile(features, (10, 1))
        
        return observation