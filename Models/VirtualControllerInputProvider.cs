using System;
using System.Diagnostics;
using System.Windows;
using System.Threading.Tasks;

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

            switch (args.InputType)
            {
                case ControllerInputType.ThumbstickRelease:
                    HandleThumbstickRelease(args);
                    break;
                case ControllerInputType.Thumbstick:
                    InputChanged?.Invoke(this, args);
                    break;
                default:
                    InputChanged?.Invoke(this, args);
                    break;
            }
        }

        private async void HandleThumbstickRelease(ControllerInputEventArgs args)
        {
            try
            {
                dynamic releaseValue = args.Value;
                Point releasePos = releaseValue.ReleasePosition;
                string stickName = args.InputName;

                // Get the actual last position
                short startX = Convert.ToInt16(releaseValue.X);
                short startY = Convert.ToInt16(releaseValue.Y);

                const int STEPS = 20;
                for (int i = STEPS - 1; i >= 0; i--)
                {
                    double t = i / (double)STEPS;
                    
                    // Calculate current step values
                    short currentX = (short)(startX * t);
                    short currentY = (short)(startY * t);

                    var stepEvent = new ControllerInputEventArgs(
                        ControllerInputType.Thumbstick,
                        stickName,
                        new { 
                            X = currentX,
                            Y = currentY,
                            Pressed = false 
                        }
                    );

                    InputChanged?.Invoke(this, stepEvent);
                    Debug.WriteLine($"Spring-back step: {stickName} X={currentX} Y={currentY}");

                    await Task.Delay(16); // ~60fps timing
                }

                // Final center position
                var finalEvent = new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    stickName,
                    new { X = (short)0, Y = (short)0, Pressed = false }
                );

                InputChanged?.Invoke(this, finalEvent);
                Debug.WriteLine($"Spring-back complete: {stickName} at center");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in spring-back: {ex.Message}");
            }
        }
    }
}