// Referenced in:
// - MainWindow.xaml.cs

using System;
using System.Windows;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace XB2Midi.Models
{
    public class TestControllerSimulator
    {
        private readonly Dictionary<string, DispatcherTimer> thumbstickTimers = new();
        private readonly Dictionary<string, Point> thumbstickPositions = new();
        
        public event EventHandler<ControllerInputEventArgs>? SimulatedInput;
        
        // Define spring-back rate (how fast thumbsticks return to center)
        public double SpringBackRate { get; set; } = 0.05;
        
        // Timer interval in milliseconds
        private const int TIMER_INTERVAL_MS = 16; // ~60fps
        
        public TestControllerSimulator()
        {
            Debug.WriteLine("TestControllerSimulator initialized");
        }
        
        public void SimulateStickRelease(string stickName, Point releasePosition)
        {
            Debug.WriteLine($"SimulateStickRelease: {stickName} from position {releasePosition.X:F2}, {releasePosition.Y:F2}");
            
            // Store the current position
            thumbstickPositions[stickName] = releasePosition;
            
            // Cancel any existing spring-back animation for this stick
            if (thumbstickTimers.TryGetValue(stickName, out var existingTimer) && existingTimer != null)
            {
                existingTimer.Stop();
                thumbstickTimers.Remove(stickName);
            }

            // Create a new timer for this stick's spring-back animation
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
            };

            // Keep track of the current position during animation
            Point currentPos = releasePosition;
            
            timer.Tick += (s, e) =>
            {
                // Calculate new position with spring effect
                double newX = currentPos.X * (1.0 - SpringBackRate);
                double newY = currentPos.Y * (1.0 - SpringBackRate);
                currentPos = new Point(newX, newY);
                
                // Convert to XInput range
                short xInput = (short)(newX * 32767);
                short yInput = (short)(newY * 32767);
                
                // Raise event with new position
                SimulatedInput?.Invoke(this, new ControllerInputEventArgs(
                    ControllerInputType.Thumbstick,
                    stickName,
                    new { X = xInput, Y = yInput }
                ));
                
                Debug.WriteLine($"Spring-back: {stickName} Position: {newX:F3}, {newY:F3}");
                
                // Check if we should stop (close enough to center)
                if (Math.Abs(newX) < 0.01 && Math.Abs(newY) < 0.01)
                {
                    timer.Stop();
                    thumbstickTimers.Remove(stickName);
                    
                    // Send final center position with exact zeros to ensure precise centering
                    SimulatedInput?.Invoke(this, new ControllerInputEventArgs(
                        ControllerInputType.Thumbstick,
                        stickName,
                        new { X = (short)0, Y = (short)0 }
                    ));
                    
                    Debug.WriteLine($"Spring-back completed for {stickName}");
                }
            };
            
            // Store and start the timer
            thumbstickTimers[stickName] = timer;
            timer.Start();
        }
    }
}