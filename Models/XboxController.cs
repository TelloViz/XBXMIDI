using System;
using SharpDX.XInput;

namespace XB2Midi.Models
{
    public class XboxController : IDisposable
    {
        // Increase deadzone threshold to 8%
        private const float DEADZONE_THRESHOLD = 0.08f;
        private const short STICK_MAX_VALUE = 32767;
        private const short STICK_MIN_VALUE = -32768;

        private readonly Controller controller;
        private State previousState;
        private bool disposed;

        public event EventHandler<ControllerInputEventArgs>? InputChanged;

        public XboxController()
        {
            controller = new Controller(UserIndex.One);
            if (!controller.IsConnected)
                throw new InvalidOperationException("Xbox controller not connected");
            
            previousState = controller.GetState();
        }

        public void Update()
        {
            if (disposed) return;

            var currentState = controller.GetState();
            if (currentState.PacketNumber != previousState.PacketNumber)
            {
                // Raise events for changed inputs
                CheckButtons(currentState, previousState);
                CheckTriggers(currentState, previousState);
                CheckThumbSticks(currentState, previousState);
                
                previousState = currentState;
            }
        }

        private void CheckButtons(State current, State previous)
        {
            if (current.Gamepad.Buttons != previous.Gamepad.Buttons)
            {
                // Check each button individually
                CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.A, "A");
                CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.B, "B");
                CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.X, "X");
                CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.Y, "Y");
            }
        }

        private void CheckButton(GamepadButtonFlags currentButtons, GamepadButtonFlags button, string buttonName)
        {
            bool isPressed = (currentButtons & button) == button;
            InputChanged?.Invoke(this, new ControllerInputEventArgs(
                ControllerInputType.Button,
                buttonName,
                isPressed ? 1 : 0
            ));
        }

        private void CheckTriggers(State current, State previous)
        {
            if (current.Gamepad.LeftTrigger != previous.Gamepad.LeftTrigger)
            {
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Trigger,
                    "LeftTrigger",
                    current.Gamepad.LeftTrigger
                ));
            }

            if (current.Gamepad.RightTrigger != previous.Gamepad.RightTrigger)
            {
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Trigger,
                    "RightTrigger",
                    current.Gamepad.RightTrigger
                ));
            }
        }

        private void CheckThumbSticks(State current, State previous)
        {
            var leftStick = ApplyDeadzone(
                current.Gamepad.LeftThumbX, 
                current.Gamepad.LeftThumbY
            );

            var rightStick = ApplyDeadzone(
                current.Gamepad.RightThumbX, 
                current.Gamepad.RightThumbY
            );

            if (leftStick.X != previous.Gamepad.LeftThumbX ||
                leftStick.Y != previous.Gamepad.LeftThumbY)
            {
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    "LeftThumbstick",
                    new { X = leftStick.X, Y = leftStick.Y }
                ));
            }

            if (rightStick.X != previous.Gamepad.RightThumbX ||
                rightStick.Y != previous.Gamepad.RightThumbY)
            {
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    "RightThumbstick",
                    new { X = rightStick.X, Y = rightStick.Y }
                ));
            }
        }

        private (short X, short Y) ApplyDeadzone(short x, short y)
        {
            // Convert to float for easier calculations
            float normalizedX = x / (float)STICK_MAX_VALUE;
            float normalizedY = y / (float)STICK_MAX_VALUE;

            // Calculate the distance from center (magnitude)
            float magnitude = MathF.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);

            // If magnitude is less than deadzone, return zero
            if (magnitude < DEADZONE_THRESHOLD)
            {
                return (0, 0);
            }

            // Calculate the normalized direction
            float normalizedMagnitude = Math.Min(magnitude, 1.0f);
            normalizedX /= magnitude;
            normalizedY /= magnitude;

            // Scale the normalized direction by the normalized magnitude
            normalizedX *= normalizedMagnitude;
            normalizedY *= normalizedMagnitude;

            // Convert back to short values
            return (
                (short)(normalizedX * STICK_MAX_VALUE),
                (short)(normalizedY * STICK_MAX_VALUE)
            );
        }

        public void Dispose()
        {
            disposed = true;
        }
    }

    public class ControllerInputEventArgs : EventArgs
    {
        public ControllerInputType InputType { get; }
        public string InputName { get; }
        public object Value { get; }

        public ControllerInputEventArgs(ControllerInputType type, string name, object value)
        {
            InputType = type;
            InputName = name;
            Value = value;
        }
    }

    public enum ControllerInputType
    {
        Button,
        Trigger,
        Thumbstick
    }
}