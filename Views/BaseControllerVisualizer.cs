using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XB2Midi.Models;
using System.Collections.Generic;

namespace XB2Midi.Views
{
    public abstract class BaseControllerVisualizer : UserControl
    {
        protected const double MAX_RADIUS = 35.0;
        protected const double CENTER_OFFSET = 35.0;

        // Dictionary to store original button colors
        protected static readonly Dictionary<string, Brush> originalButtonColors = new Dictionary<string, Brush>
        {
            // Face buttons with their standard colors
            { "A", Brushes.LimeGreen },
            { "B", Brushes.Red },
            { "X", Brushes.Blue },
            { "Y", Brushes.Yellow },
            
            // Other buttons with standard gray
            { "LeftBumper", Brushes.Gray },
            { "RightBumper", Brushes.Gray },
            { "Back", Brushes.Gray },
            { "Start", Brushes.Gray },
            { "DPadUp", Brushes.Gray },
            { "DPadDown", Brushes.Gray },
            { "DPadLeft", Brushes.Gray },
            { "DPadRight", Brushes.Gray },
            { "LeftThumbClick", Brushes.Gray },
            { "RightThumbClick", Brushes.Gray },
            
            // Thumbsticks with dark gray
            { "LeftThumbstick", Brushes.DarkGray },
            { "RightThumbstick", Brushes.DarkGray }
        };

        public virtual event EventHandler<ControllerInputEventArgs>? SimulateInput;
        
        public virtual double TriggerRate { get; set; } = 5.0;

        public virtual void UpdateControl(ControllerInputEventArgs e)
        {
            switch (e.InputType)
            {
                case ControllerInputType.Button:
                    UpdateButtonVisual(e.InputName, Convert.ToBoolean(e.Value));
                    break;
                case ControllerInputType.Trigger:
                    UpdateTriggerVisual(e.InputName, Convert.ToByte(e.Value));
                    break;
                case ControllerInputType.Thumbstick:
                    UpdateThumbstickVisual(e.InputName, e.Value);
                    break;
            }
        }

        public virtual void UpdateModeLEDs(ControllerMode mode)
        {
            // Turn off all LEDs first
            for (int i = 1; i <= 4; i++)
            {
                var led = FindName($"LED{i}") as Ellipse;
                if (led != null)
                {
                    led.Fill = Brushes.DarkGray;
                }
            }

            // Light up the LED corresponding to current mode (modes start at 0, LEDs at 1)
            var currentLed = FindName($"LED{(int)mode + 1}") as Ellipse;
            if (currentLed != null)
            {
                currentLed.Fill = Brushes.LimeGreen;
            }
        }

        protected virtual void UpdateThumbstickVisual(string name, object value)
        {
            // Step 1: Determine which thumbstick we need to update based on the input name
            string thumbstickName = name;
            
            // Handle component-wise updates (e.g., "LeftThumbstickX") by extracting the base name
            if (name.EndsWith("X") || name.EndsWith("Y"))
            {
                thumbstickName = name.Substring(0, name.Length - 1);
            }
            
            // Step 2: Find the UI element for the thumbstick
            var thumbstick = FindName(thumbstickName) as Border;
            if (thumbstick == null)
            {
                // If we couldn't find with the exact name, try alternative formats
                if (thumbstickName == "LeftThumbstick")
                    thumbstick = FindName("LeftThumbstick") as Border;
                else if (thumbstickName == "RightThumbstick")
                    thumbstick = FindName("RightThumbstick") as Border;
                    
                if (thumbstick == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VISUALIZER] Could not find thumbstick element: {thumbstickName}");
                    return;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[VISUALIZER] Updating thumbstick {thumbstickName} with value: {value}");
            
            try
            {
                // Step 3: Extract X and Y coordinates from the value object
                dynamic stick = value;
                short xValue;
                short yValue;
                
                // Extract X/Y values based on the input format
                if (name.EndsWith("X"))
                {
                    // For individual axis updates, extract just that component
                    xValue = stick.X;
                    
                    // Get current Y position from the Canvas
                    var canvas = thumbstick.Parent as Canvas;
                    if (canvas != null)
                    {
                        double currentY = Canvas.GetTop(thumbstick) - CENTER_OFFSET;
                        yValue = (short)(currentY * -32767.0 / MAX_RADIUS); // Convert back from UI to controller range
                    }
                    else
                    {
                        yValue = 0;
                    }
                }
                else if (name.EndsWith("Y"))
                {
                    // For Y axis updates, use current X position
                    var canvas = thumbstick.Parent as Canvas;
                    if (canvas != null)
                    {
                        double currentX = Canvas.GetLeft(thumbstick) - CENTER_OFFSET;
                        xValue = (short)(currentX * 32767.0 / MAX_RADIUS); // Convert back from UI to controller range
                    }
                    else
                    {
                        xValue = 0;
                    }
                    
                    yValue = stick.Y;
                }
                else
                {
                    // For combined updates, use both components
                    xValue = stick.X;
                    yValue = stick.Y;
                }

                // Step 4: Convert controller values (-32768 to 32767) to canvas coordinates
                double x = xValue / 32767.0 * MAX_RADIUS;
                double y = -yValue / 32767.0 * MAX_RADIUS; // Negative Y for correct direction in UI
                
                System.Diagnostics.Debug.WriteLine($"[VISUALIZER] Setting thumbstick position: X={x:F2}, Y={y:F2}");

                // Step 5: Position the thumbstick on the canvas
                Canvas.SetLeft(thumbstick, CENTER_OFFSET + x);
                Canvas.SetTop(thumbstick, CENTER_OFFSET + y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VISUALIZER] Error updating thumbstick: {ex.Message}");
            }
        }

        protected virtual void UpdateButtonVisual(string name, bool isPressed)
        {
            var button = FindName(name) as Border;
            if (button == null) return;

            if (isPressed)
            {
                // When pressed, highlight with light green
                button.Background = Brushes.LightGreen;
            }
            else
            {
                // When released, restore original color
                if (originalButtonColors.TryGetValue(name, out var originalColor))
                {
                    button.Background = originalColor;
                }
                else
                {
                    // Fallback for any buttons not explicitly defined
                    button.Background = name.Contains("Thumbstick") ? Brushes.DarkGray : Brushes.Gray;
                }
            }
        }

        protected virtual void UpdateTriggerVisual(string name, byte value)
        {
            var trigger = FindName(name + "Value") as ProgressBar;
            if (trigger == null) return;

            trigger.Value = value / 255.0 * 100;
        }
    }
}