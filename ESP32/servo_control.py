from machine import Pin, PWM
import time

# FS90R control parameters
FREQ = 50  # 50 Hz for standard servo
MIN_PULSE = 1000  # µs
NEUTRAL_PULSE = 1500  # µs (stop)
MAX_PULSE = 2000  # µs
PULSE_RANGE = MAX_PULSE - NEUTRAL_PULSE  # 500 µs range for full speed
PERIOD = 1000000 // FREQ  # period in µs (20_000 µs)
MAX_DUTY = 1023  # 10-bit PWM on ESP32

class FS90R:
    def __init__(self, pin_num):
        self.pwm = PWM(Pin(pin_num), freq=FREQ)

    def set_speed(self, speed_percent):
        # speed_percent: -100 (full reverse) to 100 (full forward)
        spd = max(-100, min(100, speed_percent))
        pulse = NEUTRAL_PULSE + (PULSE_RANGE * spd // 100)
        duty = int(pulse * MAX_DUTY // PERIOD)
        self.pwm.duty(duty)

    def stop(self):
        self.set_speed(0)

if __name__ == '__main__':
    # Update pin number to match your wiring
    servo = FS90R(pin_num=14)
    try:
        while True:
            inp = input('Enter speed (-100 to 100), blank to stop: ')
            if not inp:
                servo.stop()
                break
            try:
                val = int(inp)
            except ValueError:
                print('Invalid value')
                continue
            servo.set_speed(val)
    except KeyboardInterrupt:
        servo.stop()
        print('Interrupted, servo stopped.')

# End of servo_control.py