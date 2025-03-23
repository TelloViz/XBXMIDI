using System;
using SharpDX.XInput;

namespace XB2Midi.Models
{
    public class XboxController : IDisposable
    {
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
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Button,
                    current.Gamepad.Buttons.ToString(),
                    current.Gamepad.Buttons
                ));
            }
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
            if (current.Gamepad.LeftThumbX != previous.Gamepad.LeftThumbX ||
                current.Gamepad.LeftThumbY != previous.Gamepad.LeftThumbY)
            {
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    "LeftThumbstick",
                    new { X = current.Gamepad.LeftThumbX, Y = current.Gamepad.LeftThumbY }
                ));
            }

            if (current.Gamepad.RightThumbX != previous.Gamepad.RightThumbX ||
                current.Gamepad.RightThumbY != previous.Gamepad.RightThumbY)
            {
                InputChanged?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    "RightThumbstick",
                    new { X = current.Gamepad.RightThumbX, Y = current.Gamepad.RightThumbY }
                ));
            }
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