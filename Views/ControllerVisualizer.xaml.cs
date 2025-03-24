using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpDX.XInput; // Add this for GamepadButtonFlags
using XB2Midi.Models;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for ControllerVisualizer.xaml
    /// </summary>
    public partial class ControllerVisualizer : UserControl
    {
        public ControllerVisualizer()
        {
            InitializeComponent();
        }

        public void UpdateControl(ControllerInputEventArgs e)
        {
            if (e == null) return;

            Dispatcher.Invoke(() =>
            {
                switch (e.InputType)
                {
                    case ControllerInputType.Button:
                        UpdateButton(e.InputName, e.Value);
                        break;
                    case ControllerInputType.Trigger:
                        UpdateTrigger(e.InputName, e.Value);
                        break;
                    case ControllerInputType.Thumbstick:
                        UpdateThumbstick(e.InputName, e.Value);
                        break;
                }
            });
        }

        private void UpdateButton(string name, object value)
        {
            // Convert thumb click names to corresponding thumbstick names
            string controlName = name switch
            {
                "LeftThumbClick" => "LeftThumbstick",
                "RightThumbClick" => "RightThumbstick",
                _ => name
            };

            var border = FindName(controlName) as Border;
            if (border == null) return;

            bool isPressed;
            if (value is GamepadButtonFlags flags)
            {
                isPressed = flags != GamepadButtonFlags.None;
            }
            else
            {
                isPressed = value?.ToString() != "0";
            }

            border.Background = isPressed ? 
                Brushes.LightGreen : 
                (controlName.Contains("Thumbstick") ? Brushes.DarkGray : Brushes.Gray);
        }

        private void UpdateTrigger(string name, object value)
        {
            var progress = FindName($"{name}Value") as ProgressBar;
            if (progress == null) return;

            if (value is byte byteValue)
            {
                progress.Value = byteValue / 255.0 * 100;
            }
        }

        private void UpdateThumbstick(string name, object value)
        {
            var thumb = FindName(name) as Border;
            if (thumb == null) return;

            if (value is { } pos)
            {
                // Calculate center position of container (100x100) minus half of thumb size (30x30)
                const double centerOffset = (100 - 30) / 2;
                
                // Convert XInput values (-32768 to 32767) to canvas coordinates
                double x = ((dynamic)pos).X / 32767.0 * 35; // Scale by 35 to keep within bounds
                double y = -((dynamic)pos).Y / 32767.0 * 35; // Negative Y for correct direction
                
                // Set position relative to center
                Canvas.SetLeft(thumb, centerOffset + x);
                Canvas.SetTop(thumb, centerOffset + y);

                // Handle L3/R3 button presses
                bool isPressed = ((dynamic)pos).Pressed;
                thumb.Background = isPressed ? Brushes.LightGreen : Brushes.DarkGray;
            }
        }
    }
}