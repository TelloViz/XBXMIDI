using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input; 
using System.Windows.Shapes;
using System.Diagnostics;
using SharpDX.XInput;
using XB2Midi.Models;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for ControllerVisualizer.xaml
    /// </summary>
    public partial class ControllerVisualizer : BaseControllerVisualizer
    {
        // Keep existing XAML-related initialization
        public ControllerVisualizer()
        {
            InitializeComponent();
        }

        public override void UpdateControl(ControllerInputEventArgs e)
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

        // Implement required abstract methods
        protected override void UpdateButtonVisual(string name, bool isPressed)
        {
            // Find the button element by name and update its appearance
            var button = FindName(name) as Button;
            if (button != null)
            {
                button.Background = isPressed ? 
                    new SolidColorBrush(Colors.Red) : 
                    new SolidColorBrush(Colors.LightGray);
            }
            
            // Special case for bumpers/triggers that might have different naming
            if (name == "LeftShoulder" || name == "RightShoulder" || 
                name == "LeftTrigger" || name == "RightTrigger")
            {
                var alt = FindName(name + "Button") as Button;
                if (alt != null)
                {
                    alt.Background = isPressed ? 
                        new SolidColorBrush(Colors.Red) : 
                        new SolidColorBrush(Colors.LightGray);
                }
            }
        }

        protected override void UpdateTriggerVisual(string name, byte value)
        {
            // Find the trigger progress bar by name and update its value
            var trigger = FindName(name + "Bar") as ProgressBar;
            if (trigger != null)
            {
                trigger.Value = value;
            }
        }

        protected override void UpdateThumbstickVisual(string name, object value)
        {
            try
            {
                // Try to get the Canvas for this thumbstick
                var canvas = FindName(name + "Canvas") as Canvas;
                var thumb = FindName(name + "Thumb") as Ellipse;
                
                if (canvas != null && thumb != null && value != null)
                {
                    // Extract X and Y values
                    dynamic stickValue = value;
                    double normalizedX = Convert.ToDouble(stickValue.X) / 32768.0;
                    double normalizedY = Convert.ToDouble(stickValue.Y) / 32768.0;
                    
                    // Assuming the max radius and center offset come from BaseVisualizer
                    double posX = CENTER_OFFSET + (normalizedX * MAX_RADIUS);
                    double posY = CENTER_OFFSET - (normalizedY * MAX_RADIUS);
                    
                    // Update position of the thumbstick
                    Canvas.SetLeft(thumb, posX - thumb.Width / 2);
                    Canvas.SetTop(thumb, posY - thumb.Height / 2);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating thumbstick visual: {ex.Message}");
            }
        }
    }
}