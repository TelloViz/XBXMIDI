using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XB2Midi.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace XB2Midi.Views
{
    public class InteractiveControllerVisualizer : ControllerVisualizer
    {
        private Border? draggedThumbstick;
        private Canvas? dragCanvas;
        private Point dragStart;
        private ProgressBar? activeTrigger;
        private System.Windows.Threading.DispatcherTimer? triggerTimer;
        private double lastTriggerValue = -1;
        private const int TIMER_INTERVAL_MS = 16;

        // Add override keyword to inherited members
        public override event EventHandler<ControllerInputEventArgs>? SimulateInput;
        
        public override double TriggerRate 
        { 
            get => base.TriggerRate;
            set => base.TriggerRate = value;
        }

        public InteractiveControllerVisualizer() : base()
        {
            SetupInteractivity();
        }

        private void SetupInteractivity()
        {
            // Setup necessary after template is applied
            Loaded += (s, e) => {
                SetupButtonEvents();
                SetupThumbstickEvents();
                SetupTriggerEvents();
            };
        }

        private void SetupButtonEvents()
        {
            // Dictionary mapping element names to controller button names
            var buttonMappings = new Dictionary<string, string>
            {
                { "A", "A" },
                { "B", "B" },
                { "X", "X" },
                { "Y", "Y" },
                { "LeftBumper", "LeftBumper" },
                { "RightBumper", "RightBumper" },
                { "Back", "Back" },
                { "Start", "Start" },
                { "DPadUp", "DPadUp" },
                { "DPadDown", "DPadDown" },
                { "DPadLeft", "DPadLeft" },
                { "DPadRight", "DPadRight" }
            };

            // For each button, set up mouse events
            foreach (var mapping in buttonMappings)
            {
                var button = FindName(mapping.Key) as Border;
                if (button == null) continue;

                button.MouseLeftButtonDown += (s, e) => {
                    Debug.WriteLine($"Interactive button down: {mapping.Value}");
                    RaiseInputEvent(ControllerInputType.Button, mapping.Value, 1);
                    e.Handled = true;
                };

                button.MouseLeftButtonUp += (s, e) => {
                    Debug.WriteLine($"Interactive button up: {mapping.Value}");
                    RaiseInputEvent(ControllerInputType.Button, mapping.Value, 0);
                    e.Handled = true;
                };

                // Make the button look interactive
                button.Cursor = Cursors.Hand;
            }

            // Setup thumbstick clicks
            SetupThumbstickClickEvents("LeftThumbstick", "LeftThumbClick");
            SetupThumbstickClickEvents("RightThumbstick", "RightThumbClick");
        }

        private void SetupThumbstickEvents()
        {
            // Setup left thumbstick
            var leftStick = FindName("LeftThumbstick") as Border;
            var leftCanvas = leftStick?.Parent as Canvas;
            if (leftStick != null && leftCanvas != null)
                SetupThumbstickDrag(leftStick, leftCanvas, "LeftThumbstick");

            // Setup right thumbstick
            var rightStick = FindName("RightThumbstick") as Border;
            var rightCanvas = rightStick?.Parent as Canvas;
            if (rightStick != null && rightCanvas != null)
                SetupThumbstickDrag(rightStick, rightCanvas, "RightThumbstick");
        }

        private void SetupThumbstickDrag(Border stick, Canvas canvas, string stickName)
        {
            // Make the thumbstick draggable
            stick.Cursor = Cursors.SizeAll;
            
            // Initialize thumbstick at center
            Canvas.SetLeft(stick, (canvas.Width - stick.Width) / 2);
            Canvas.SetTop(stick, (canvas.Height - stick.Height) / 2);

            stick.MouseLeftButtonDown += (s, e) => {
                draggedThumbstick = stick;
                dragCanvas = canvas;
                dragStart = e.GetPosition(canvas);
                stick.CaptureMouse();
                e.Handled = true;
            };

            stick.MouseMove += (s, e) => {
                if (draggedThumbstick != stick) return;
                
                Point currentPos = e.GetPosition(canvas);

                // Calculate position within the canvas bounds
                double centerX = canvas.Width / 2;
                double centerY = canvas.Height / 2;
                double radiusX = centerX - stick.Width / 2;
                double radiusY = centerY - stick.Height / 2;

                // Calculate vector from center
                double dx = currentPos.X - centerX;
                double dy = currentPos.Y - centerY;
                double length = Math.Sqrt(dx * dx + dy * dy);

                // Normalize if outside unit circle
                if (length > radiusX)
                {
                    dx = dx / length * radiusX;
                    dy = dy / length * radiusY;
                }

                // Position the thumbstick
                Canvas.SetLeft(stick, centerX + dx - stick.Width / 2);
                Canvas.SetTop(stick, centerY + dy - stick.Height / 2);

                // Calculate normalized values (-1 to 1)
                double normX = dx / radiusX;
                double normY = -dy / radiusY;  // Y is inverted in screen coordinates
                
                Debug.WriteLine($"Normalized position: {normX:F2}, {normY:F2}");

                // Convert to XInput range (-32768 to 32767)
                short xInput = (short)(normX * 32767);
                short yInput = (short)(normY * 32767);

                // Raise input event for the thumbstick
                RaiseInputEvent(ControllerInputType.Thumbstick, stickName, new { X = xInput, Y = yInput });
                e.Handled = true;
            };

            stick.MouseLeftButtonUp += (s, e) => {
                if (draggedThumbstick != stick) return;

                // Get current normalized position
                double centerX = canvas.Width / 2;
                double centerY = canvas.Height / 2;
                double radiusX = centerX - stick.Width / 2;
                double radiusY = centerY - stick.Height / 2;
                
                double stickLeft = Canvas.GetLeft(stick);
                double stickTop = Canvas.GetTop(stick);
                
                double dx = (stickLeft + stick.Width / 2) - centerX;
                double dy = (stickTop + stick.Height / 2) - centerY;
                
                double normX = dx / radiusX;
                double normY = -dy / radiusY;  // Y is inverted in screen coordinates

                // Release the mouse and reset drag state
                stick.ReleaseMouseCapture();
                draggedThumbstick = null;
                
                // Generate thumbstick release event with current position
                Debug.WriteLine($"Raising ThumbstickRelease event for {stickName} at {normX:F2}, {normY:F2}");
                RaiseInputEvent(ControllerInputType.ThumbstickRelease, stickName, 
                    new { ReleasePosition = new Point(normX, normY) });

                e.Handled = true;
            };
        }

        private void SetupThumbstickClickEvents(string stickName, string clickName)
        {
            var stick = FindName(stickName) as Border;
            if (stick == null) return;

            stick.MouseRightButtonDown += (s, e) => {
                Debug.WriteLine($"Interactive thumbstick click: {clickName}");
                RaiseInputEvent(ControllerInputType.Button, clickName, 1);
                e.Handled = true;
            };

            stick.MouseRightButtonUp += (s, e) => {
                Debug.WriteLine($"Interactive thumbstick release: {clickName}");
                RaiseInputEvent(ControllerInputType.Button, clickName, 0);
                e.Handled = true;
            };
        }

        private void SetupTriggerEvents()
        {
            SetupTriggerEvents("LeftTrigger");
            SetupTriggerEvents("RightTrigger");

            // Initialize trigger timer
            triggerTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
            };
            triggerTimer.Tick += TriggerTimer_Tick;
        }

        private void SetupTriggerEvents(string triggerName)
        {
            var trigger = FindName($"{triggerName}Value") as ProgressBar;
            if (trigger == null) return;

            trigger.MouseLeftButtonDown += (s, e) =>
            {
                activeTrigger = trigger;
                triggerTimer?.Start();
                e.Handled = true;
            };

            trigger.MouseLeftButtonUp += (s, e) =>
            {
                if (activeTrigger == trigger)
                {
                    activeTrigger = null;
                    triggerTimer?.Stop();
                    ReleaseTrigger(triggerName);
                }
            };

            trigger.Cursor = Cursors.Hand;
        }

        private void TriggerTimer_Tick(object? sender, EventArgs e)
        {
            if (activeTrigger == null) return;

            string triggerName = activeTrigger.Name.Replace("Value", "");
            double newValue = Math.Min(activeTrigger.Value + TriggerRate, 100);

            if (Math.Abs(newValue - lastTriggerValue) > 0.01)
            {
                activeTrigger.Value = newValue;
                lastTriggerValue = newValue;

                byte mappedValue = (byte)(newValue * 255 / 100);
                RaiseInputEvent(ControllerInputType.Trigger, triggerName, mappedValue);
            }
        }

        private void ReleaseTrigger(string triggerName)
        {
            var trigger = FindName($"{triggerName}Value") as ProgressBar;
            if (trigger == null) return;

            var releaseTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
            };

            releaseTimer.Tick += (s, e) =>
            {
                double newValue = Math.Max(trigger.Value - TriggerRate, 0);

                if (Math.Abs(newValue - lastTriggerValue) > 0.01)
                {
                    trigger.Value = newValue;
                    lastTriggerValue = newValue;

                    byte mappedValue = (byte)(newValue * 255 / 100);
                    RaiseInputEvent(ControllerInputType.Trigger, triggerName, mappedValue);
                }

                if (newValue <= 0)
                {
                    releaseTimer.Stop();
                    lastTriggerValue = -1;
                }
            };

            releaseTimer.Start();
        }

        private void RaiseInputEvent(ControllerInputType type, string name, object value)
        {
            Debug.WriteLine($"Raising input event: {type} {name} = {value}");
            SimulateInput?.Invoke(this, new ControllerInputEventArgs(type, name, value));
        }

        protected override void UpdateThumbstickVisual(string name, object value)
        {
            base.UpdateThumbstickVisual(name, value);
            
            // Add any additional visual feedback for interactive mode
            var thumb = FindName(name) as Border;
            if (thumb != null)
            {
                thumb.Cursor = Cursors.Hand;
            }
        }
    }
}