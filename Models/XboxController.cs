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

        // Add new property to track connection status
        public bool IsConnected => controller?.IsConnected ?? false;

        private readonly Controller controller;
        private State previousState;
        private bool disposed;

        public event EventHandler<ControllerInputEventArgs>? InputChanged;
        // Add new event for connection status changes
        public event EventHandler<bool>? ConnectionChanged;

        public XboxController()
        {
            controller = new Controller(UserIndex.One);
            // Remove the connection check/exception
            previousState = default;
        }

        public void Update()
        {
            if (disposed) return;

            // Check if connection status changed
            bool wasConnected = previousState.PacketNumber != 0;
            bool isNowConnected = controller.IsConnected;
            
            if (wasConnected != isNowConnected)
            {
                ConnectionChanged?.Invoke(this, isNowConnected);
                if (!isNowConnected)
                {
                    previousState = default;
                    return;
                }
            }

            // Only proceed if controller is connected
            if (!controller.IsConnected) return;

            var currentState = controller.GetState();
            if (currentState.PacketNumber != previousState.PacketNumber)
            {
                // Only check buttons if buttons changed
                if (currentState.Gamepad.Buttons != previousState.Gamepad.Buttons)
                {
                    CheckButtons(currentState, previousState);
                }

                // Only check triggers if triggers changed
                if (currentState.Gamepad.LeftTrigger != previousState.Gamepad.LeftTrigger ||
                    currentState.Gamepad.RightTrigger != previousState.Gamepad.RightTrigger)
                {
                    CheckTriggers(currentState, previousState);
                }

                // Only check thumbsticks if their values changed
                if (currentState.Gamepad.LeftThumbX != previousState.Gamepad.LeftThumbX ||
                    currentState.Gamepad.LeftThumbY != previousState.Gamepad.LeftThumbY ||
                    currentState.Gamepad.RightThumbX != previousState.Gamepad.RightThumbX ||
                    currentState.Gamepad.RightThumbY != previousState.Gamepad.RightThumbY)
                {
                    CheckThumbSticks(currentState, previousState);
                }
                
                previousState = currentState;
            }
        }

        private void CheckButtons(State current, State previous)
        {
            var changed = current.Gamepad.Buttons ^ previous.Gamepad.Buttons;
            if (changed != 0)
            {
                // Only check buttons that actually changed state
                if ((changed & GamepadButtonFlags.A) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.A, "A");
                if ((changed & GamepadButtonFlags.B) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.B, "B");
                if ((changed & GamepadButtonFlags.X) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.X, "X");
                if ((changed & GamepadButtonFlags.Y) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.Y, "Y");
                if ((changed & GamepadButtonFlags.LeftShoulder) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.LeftShoulder, "LeftBumper");
                if ((changed & GamepadButtonFlags.RightShoulder) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.RightShoulder, "RightBumper");
                if ((changed & GamepadButtonFlags.Start) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.Start, "Start");
                if ((changed & GamepadButtonFlags.Back) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.Back, "Back");
                if ((changed & GamepadButtonFlags.DPadUp) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.DPadUp, "DPadUp");
                if ((changed & GamepadButtonFlags.DPadDown) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.DPadDown, "DPadDown");
                if ((changed & GamepadButtonFlags.DPadLeft) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.DPadLeft, "DPadLeft");
                if ((changed & GamepadButtonFlags.DPadRight) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.DPadRight, "DPadRight");
                if ((changed & GamepadButtonFlags.LeftThumb) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.LeftThumb, "LeftThumbClick");
                if ((changed & GamepadButtonFlags.RightThumb) != 0)
                    CheckButton(current.Gamepad.Buttons, GamepadButtonFlags.RightThumb, "RightThumbClick");
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