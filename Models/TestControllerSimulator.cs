using System;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace XB2Midi.Models
{
    public class TestControllerSimulator
    {
        private readonly DispatcherTimer springBackTimer;
        private const int SPRING_INTERVAL_MS = 16;
        private double springBackRate = 0.15;
        private Point currentPosition;

#pragma warning disable CS0649 // Field is never assigned to
        private string? currentStick;  // Make nullable
#pragma warning restore CS0649

        private bool isReturning;
        private const double STICK_RANGE = 1.0; // Normalized range for stick movement

        public TestControllerSimulator()
        {
            springBackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SPRING_INTERVAL_MS)
            };
            springBackTimer.Tick += SpringBack_Tick;
            currentPosition = new Point(0, 0);
            System.Diagnostics.Debug.WriteLine("TestControllerSimulator initialized");
        }

        public double SpringBackRate
        {
            get => springBackRate;
            set 
            {
                springBackRate = Math.Clamp(value, 0.001, 0.5);
                System.Diagnostics.Debug.WriteLine($"Spring-back rate updated to: {springBackRate:F3}");
            }
        }

        public async void SimulateStickRelease(string thumbstickName, Point releasePos)
        {
            const int STEPS = 20;
            for (int i = STEPS - 1; i >= 0; i--)
            {
                double t = i / (double)STEPS;
                double x = releasePos.X * t;
                double y = releasePos.Y * t;

                var e = new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    thumbstickName,
                    new { X = (short)(x * 32767), Y = (short)(y * 32767), Pressed = false }
                );

                SimulatedInput?.Invoke(this, e);
                Debug.WriteLine($"Spring-back step {i}: {thumbstickName} at X={x:F2}, Y={y:F2}");
                
                await Task.Delay(16); // ~60fps
            }

            // Final center position
            var finalEvent = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                thumbstickName,
                new { X = (short)0, Y = (short)0, Pressed = false }
            );

            SimulatedInput?.Invoke(this, finalEvent);
            Debug.WriteLine($"Spring-back complete: {thumbstickName} at center");
        }

        private void SpringBack_Tick(object? sender, EventArgs e)
        {
            if (!isReturning || currentStick == null)
            {
                System.Diagnostics.Debug.WriteLine("SpringBack_Tick skipped - not returning or no stick");
                return;
            }

            // Calculate new position with spring-back force
            double newX = currentPosition.X * (1.0 - springBackRate);
            double newY = currentPosition.Y * (1.0 - springBackRate);

            // Update current position
            currentPosition = new Point(newX, newY);

            // Convert to controller range (-32768 to 32767)
            short xValue = (short)(currentPosition.X * 32767);
            short yValue = (short)(currentPosition.Y * -32767);

            // Enhanced debug logging
            System.Diagnostics.Debug.WriteLine($"Spring-back tick - Raw: ({newX:F3}, {newY:F3}) Converted: ({xValue}, {yValue})");

            // Send unified thumbstick update
            var inputArgs = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                currentStick,
                new { X = xValue, Y = yValue, Pressed = false }
            );

            try
            {
                SimulatedInput?.Invoke(this, inputArgs);
                System.Diagnostics.Debug.WriteLine($"Input event sent for {currentStick}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending input event: {ex.Message}");
            }

            // Check if we're close enough to center to snap AFTER sending the current position
            if (Math.Abs(newX) < 0.01 && Math.Abs(newY) < 0.01)
            {
                // Snap to exact center
                currentPosition = new Point(0, 0);
                isReturning = false;
                springBackTimer.Stop();
                
                System.Diagnostics.Debug.WriteLine($"Spring-back complete - Snapping to center");
                SendCenterPosition();
            }
        }

        private void SendCenterPosition()
        {
            if (currentStick == null) return;

            System.Diagnostics.Debug.WriteLine("Sending final center position (0, 0)");
            
            var centerArgs = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                currentStick,
                new { X = (short)0, Y = (short)0, Pressed = false }
            );
            
            SimulatedInput?.Invoke(this, centerArgs);
        }

        public event EventHandler<ControllerInputEventArgs>? SimulatedInput;
    }
}