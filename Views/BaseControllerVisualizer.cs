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
            var thumbstick = FindName(name) as Border;
            if (thumbstick == null) return;

            if (value is var stickValue)
            {
                dynamic stick = stickValue;
                double x = stick.X / 32767.0 * MAX_RADIUS;
                double y = -stick.Y / 32767.0 * MAX_RADIUS;

                Canvas.SetLeft(thumbstick, CENTER_OFFSET + x);
                Canvas.SetTop(thumbstick, CENTER_OFFSET + y);
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