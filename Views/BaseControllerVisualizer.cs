using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Diagnostics; // Add this for Debug class
using XB2Midi.Models;

namespace XB2Midi.Views
{
    public abstract class BaseControllerVisualizer : UserControl
    {
        protected const double MAX_RADIUS = 35.0;
        protected const double CENTER_OFFSET = 35.0;

        #pragma warning disable CS0067 // Event is never used
        public virtual event EventHandler<ControllerInputEventArgs>? SimulateInput;
        #pragma warning restore CS0067
        
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
            Dispatcher.Invoke(() => {
                Debug.WriteLine($"Updating mode LEDs to: {mode}");
                
                // Turn off all LEDs first
                for (int i = 1; i <= 4; i++)
                {
                    var led = FindName($"LED{i}") as Ellipse;
                    if (led != null)
                    {
                        led.Fill = Brushes.DarkGray;
                        Debug.WriteLine($"Found and updated LED{i} to off");
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find LED{i}");
                    }
                }

                // Light up the LED corresponding to current mode
                int ledIndex = mode switch
                {
                    ControllerMode.Basic => 1,
                    ControllerMode.Direct => 2,
                    ControllerMode.Chord => 3,
                    ControllerMode.Arpeggio => 4,
                    _ => 1
                };
                var currentLed = FindName($"LED{ledIndex}") as Ellipse;
                if (currentLed != null)
                {
                    currentLed.Fill = Brushes.LimeGreen;
                    Debug.WriteLine($"Found and updated LED{ledIndex} to on");
                    
                    // Force visual refresh
                    currentLed.InvalidateVisual();
                }
                else
                {
                    Debug.WriteLine($"Could not find LED{ledIndex}");
                }
            });
        }
        
        // Abstract or virtual methods that derived classes should implement
        protected abstract void UpdateButtonVisual(string name, bool isPressed);
        protected abstract void UpdateTriggerVisual(string name, byte value);
        protected abstract void UpdateThumbstickVisual(string name, object value);
    }
}