import triad_openvr
import time
import sys
import struct
import socket
import threading

def tracker_thread(device_id, port, interval):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    server_address = ('127.0.0.1', port)
    
    while True:
        start = time.time()
        try:
            data = v.devices[device_id].get_pose_quaternion()
            sent = sock.sendto(struct.pack('d'*len(data), *data), server_address)
            print(f"\r{device_id}: {data}", end="")
        except Exception as e:
            print(f"\rError with {device_id}: {e}", end="")
        
        sleep_time = interval-(time.time()-start)
        if sleep_time > 0:
            time.sleep(sleep_time)

# Initialize VR
v = triad_openvr.triad_openvr()
v.print_discovered_objects()

if len(sys.argv) == 1:
    interval = 1/250
elif len(sys.argv) == 2:
    interval = 1/float(sys.argv[1])
else:
    print("Invalid number of arguments")
    interval = False

if interval:
    # Create and start threads for each tracker
    t1 = threading.Thread(target=tracker_thread, args=("tracker_1", 8051, interval))
    t2 = threading.Thread(target=tracker_thread, args=("tracker_2", 8052, interval))
    
    t1.daemon = True
    t2.daemon = True
    
    t1.start()
    t2.start()
    
    # Keep main thread alive
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\nExiting...")
        sys.exit(0)