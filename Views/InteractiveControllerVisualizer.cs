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
            // Setup button events
            SetupButtonEvents();

            // Setup thumbstick events
            SetupThumbstickEvents("LeftThumbstick");
            SetupThumbstickEvents("RightThumbstick");

            // Setup trigger events
            SetupTriggerEvents("LeftTrigger");
            SetupTriggerEvents("RightTrigger");

            // Initialize trigger timer
            triggerTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
            };
            triggerTimer.Tick += TriggerTimer_Tick;
        }

        private void SetupThumbstickEvents(string thumbstickName)
        {
            var thumb = FindName(thumbstickName) as Border;
            var canvas = thumb?.Parent as Canvas;
            if (thumb == null || canvas == null) return;

            thumb.MouseLeftButtonDown += (s, e) =>
            {
                draggedThumbstick = thumb;
                dragCanvas = canvas;
                dragStart = e.GetPosition(canvas);
                thumb.CaptureMouse();
                e.Handled = true;
            };

            thumb.MouseMove += (s, e) =>
            {
                if (draggedThumbstick == thumb && e.LeftButton == MouseButtonState.Pressed)
                {
                    Point currentPos = e.GetPosition(canvas);
                    HandleThumbstickDrag(thumbstickName, currentPos);
                }
            };

            thumb.MouseLeftButtonUp += (s, e) =>
            {
                if (draggedThumbstick == thumb)
                {
                    // Get final position before release
                    Point currentPos = e.GetPosition(canvas);
                    
                    // Calculate normalized coordinates (-1 to 1 range)
                    double centerX = canvas.ActualWidth / 2;
                    double centerY = canvas.ActualHeight / 2;
                    double deltaX = currentPos.X - centerX;
                    double deltaY = currentPos.Y - centerY;
                    
                    // Normalize if beyond max radius
                    double magnitude = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    if (magnitude > MAX_RADIUS)
                    {
                        deltaX = (deltaX / magnitude) * MAX_RADIUS;
                        deltaY = (deltaY / magnitude) * MAX_RADIUS;
                    }

                    // Calculate normalized position for spring-back
                    double normalizedX = deltaX / MAX_RADIUS;
                    double normalizedY = deltaY / MAX_RADIUS;
                    
                    // Release the mouse and clear drag state
                    thumb.ReleaseMouseCapture();
                    draggedThumbstick = null;
                    dragCanvas = null;

                    // Send release event to trigger spring-back
                    SimulateInput?.Invoke(this, new ControllerInputEventArgs(
                        ControllerInputType.ThumbstickRelease,
                        thumbstickName,
                        new { 
                            ReleasePosition = new Point(normalizedX, normalizedY) 
                        }
                    ));
                }
            };

            thumb.Cursor = Cursors.Hand;
        }

        private void HandleThumbstickDrag(string thumbstickName, Point currentPos)
        {
            if (dragCanvas == null || draggedThumbstick == null) return;

            double centerX = dragCanvas.ActualWidth / 2;
            double centerY = dragCanvas.ActualHeight / 2;
            double deltaX = currentPos.X - centerX;
            double deltaY = currentPos.Y - centerY;

            // Calculate magnitude for normalization
            double magnitude = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (magnitude > MAX_RADIUS)
            {
                deltaX = (deltaX / magnitude) * MAX_RADIUS;
                deltaY = (deltaY / magnitude) * MAX_RADIUS;
            }

            // Update visual position
            Canvas.SetLeft(draggedThumbstick, centerX + deltaX - draggedThumbstick.ActualWidth / 2);
            Canvas.SetTop(draggedThumbstick, centerY + deltaY - draggedThumbstick.ActualHeight / 2);

            // Send thumbstick position update
            SimulateInput?.Invoke(this, new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                thumbstickName,
                new { 
                    X = (short)((deltaX / MAX_RADIUS) * 32767),
                    Y = (short)((-deltaY / MAX_RADIUS) * 32767),
                    Pressed = true 
                }
            ));
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

        private void SetupButtonEvents()
        {
            // Dictionary to map button names to their MIDI mappable names
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

            foreach (var mapping in buttonMappings)
            {
                var button = FindName(mapping.Key) as Border;
                if (button == null) continue;

                button.MouseLeftButtonDown += (s, e) =>
                {
                    RaiseInputEvent(ControllerInputType.Button, mapping.Value, 1);
                    e.Handled = true;
                };

                button.MouseLeftButtonUp += (s, e) =>
                {
                    RaiseInputEvent(ControllerInputType.Button, mapping.Value, 0);
                    e.Handled = true;
                };

                // Make button interactive
                button.Cursor = Cursors.Hand;
            }

            // Setup thumbstick click (L3/R3)
            SetupThumbstickClickEvents("LeftThumbstick", "LeftThumbClick");
            SetupThumbstickClickEvents("RightThumbstick", "RightThumbClick");
        }

        private void SetupThumbstickClickEvents(string stickName, string clickName)
        {
            var stick = FindName(stickName) as Border;
            if (stick == null) return;

            stick.MouseRightButtonDown += (s, e) =>
            {
                RaiseInputEvent(ControllerInputType.Button, clickName, 1);
                e.Handled = true;
            };

            stick.MouseRightButtonUp += (s, e) =>
            {
                RaiseInputEvent(ControllerInputType.Button, clickName, 0);
                e.Handled = true;
            };
        }

        private void RaiseInputEvent(ControllerInputType type, string inputName, object value)
        {
            // First raise the event for MIDI processing
            SimulateInput?.Invoke(this, new ControllerInputEventArgs(type, inputName, value));
            Debug.WriteLine($"Raising input event: {type} {inputName} = {value}");

            // Then update the visual representation
            Dispatcher.Invoke(() =>
            {
                switch (type)
                {
                    case ControllerInputType.Button:
                        UpdateButtonVisual(inputName, Convert.ToBoolean(value));
                        break;
                        
                    case ControllerInputType.Trigger:
                        UpdateTriggerVisual(inputName, Convert.ToByte(value));
                        break;
                        
                    case ControllerInputType.Thumbstick:
                        if (inputName.EndsWith("X") || inputName.EndsWith("Y"))
                        {
                            // For individual axis updates, maintain proper visualization
                            string baseName = inputName[..^1]; // Remove X or Y
                            var thumb = FindName(baseName) as Border;
                            if (thumb == null) return;

                            // Get current position
                            double currentX = Canvas.GetLeft(thumb) - ControllerVisualizer.CENTER_OFFSET;
                            double currentY = Canvas.GetTop(thumb) - ControllerVisualizer.CENTER_OFFSET;

                            // Update appropriate axis
                            if (inputName.EndsWith("X"))
                            {
                                short xValue = Convert.ToInt16(value);
                                double normalizedX = xValue / 32767.0 * ControllerVisualizer.MAX_RADIUS;
                                Canvas.SetLeft(thumb, ControllerVisualizer.CENTER_OFFSET + normalizedX);
                            }
                            else // Y axis
                            {
                                short yValue = Convert.ToInt16(value);
                                double normalizedY = yValue / 32767.0 * ControllerVisualizer.MAX_RADIUS;
                                Canvas.SetTop(thumb, ControllerVisualizer.CENTER_OFFSET - normalizedY); // Invert Y for proper direction
                            }
                        }
                        else
                        {
                            // Direct thumbstick update (for backwards compatibility)
                            UpdateThumbstickVisual(inputName, value);
                        }
                        break;
                }
            });
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