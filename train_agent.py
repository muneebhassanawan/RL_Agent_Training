import socket
import time
import numpy as np
import gym
from stable_baselines3 import PPO
from stable_baselines3.common.env_util import make_vec_env

# Function to send movement commands to Unity
def send_command(command):
    try:
        client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client.connect(("127.0.0.1", 5005))  # Connect to Unity
        client.send(command.encode())  # Send command
        client.close()  # Close connection
    except Exception as e:
        print(f"Error Sending command: {e}")

# Custom Unity Environment for Reinforcement Learning
class UnityEnv(gym.Env):
    def __init__(self):
        super(UnityEnv, self).__init__()

        # Define 4 possible actions: Forward, Left, Right, Jump
        self.action_space = gym.spaces.Discrete(2)

        # Define dummy observation space (Modify based on actual agent state)
        self.observation_space = gym.spaces.Box(low=-10, high=10, shape=(8,), dtype=np.float32)

        self.state = np.zeros(7)  # Placeholder for agent state
        self.done = False  # Episode end flag

        self.prev_distance = 0.0  # Previous distance to target

    def step(self, action):
        actions = ["left", "right" ]
        send_command(actions[action])  # Send action to Unity
        
        time.sleep(0.1)  # Wait for Unity to update positions
        # Get updated positions from Unity
        agent_pos, target_pos, hit_wall, hit_target = self.get_positions()      
        reward = 0.0

        # Compute distance to target
        distance = np.linalg.norm(agent_pos - target_pos)

        if hit_wall:
            reward -= 5 #Penality for hitting the wall
            print(f"Reward: {reward}")
            send_command("reset") # Reset the environment
            self.done = True
        elif hit_target:
            reward += 50.0 #Reward for reaching the target
            print(f"Reward: {reward}")
            send_command("reset")
            self.done = True
        else:
            # Small reward for moving closer to the target
            reward += 0.5 * (self.prev_distance - distance)  # Reward for distance reduction
            self.prev_distance = distance  # Update previous distance
            print(f"Reward: {reward}")

        self.state = np.concatenate([agent_pos, target_pos, [float(hit_wall)], [float(hit_target)]])  # Update state])
        return self.state, reward, self.done, {}
    
    # Function to get agent and target positions from Unity
    def get_positions(self):
        try:
            client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            client.connect(("127.0.0.1", 5005))
            client.send("get_positions".encode())

            time.sleep(0.1)  # Wait for Unity to send data

            data = client.recv(1024).decode()
            client.close()

            # print(f"Received data: {data}") 

            # Parse received data
            values = data.split(",")
            if len(values) != 8:
                raise ValueError(f"Invalid data received: {data}")
            
            agent_pos = np.array([float(values[0]), float(values[1]), float(values[2])])
            target_pos = np.array([float(values[3]), float(values[4]), float(values[5])])
            hit_wall = values[6].strip().lower() == "true"
            hit_target = values[7].strip().lower() == "true"

            return agent_pos, target_pos, hit_wall, hit_target
        except Exception as e:
            print(f"Error in get_positions: {e}")
            return np.zeros(3), np.zeros(3), False, False
    

    def reset(self):
        send_command("reset") # Send reset command to Unity
        time.sleep(0.1)  # Wait for Unity to reset
        agent_pos, target_pos, hit_wall, hit_target = self.get_positions()
        self.state = np.concatenate([agent_pos, target_pos, [float(hit_wall)], [float(hit_target)]])  # Reset state
        self.done = False
        return self.state
    
    def seed(self, seed=None):
        # Set the seed for reproducibility (optional)
        np.random.seed(seed)
        return[seed]


# Create the Unity Environment
env = make_vec_env(lambda: UnityEnv(), n_envs=1)

#Load pretrained model
try:
    model = PPO.load("rl_agent", env=env)
    print("Loaded pretrained model")
except:
    model = PPO("MlpPolicy", env, verbose=1)
    print("Created new model")

model.learn(total_timesteps=1000)

# Save the trained model
model.save("rl_agent")
