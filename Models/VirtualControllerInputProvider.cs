using System;

namespace XB2Midi.Models
{
    public class VirtualControllerInputProvider : IControllerInputProvider
    {
        public event EventHandler<ControllerInputEventArgs>? InputChanged;
        public bool IsConnected => true;

        private bool isInitialized;

        public void Initialize()
        {
            isInitialized = true;
        }

        public void Update()
        {
            // Virtual controller doesn't need polling
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        public void SimulateInput(ControllerInputEventArgs args)
        {
            if (!isInitialized) return;

            // Process and normalize input before raising event
            switch (args.InputType)
            {
                case ControllerInputType.Thumbstick:
                    // Handle thumbstick input
                    if (args.Value is short shortValue)
                    {
                        // Single axis value (X or Y)
                        InputChanged?.Invoke(this, args);
                    }
                    else
                    {
                        // Full thumbstick position
                        dynamic stickValue = args.Value;
                        var xArgs = new ControllerInputEventArgs(
                            ControllerInputType.Thumbstick,
                            $"{args.InputName}X",
                            stickValue.X
                        );
                        var yArgs = new ControllerInputEventArgs(
                            ControllerInputType.Thumbstick,
                            $"{args.InputName}Y",
                            stickValue.Y
                        );

                        InputChanged?.Invoke(this, xArgs);
                        InputChanged?.Invoke(this, yArgs);
                    }
                    break;

                case ControllerInputType.Button:
                case ControllerInputType.Trigger:
                    // Pass through other input types
                    InputChanged?.Invoke(this, args);
                    break;
            }
        }
    }
}