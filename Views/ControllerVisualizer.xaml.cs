using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpDX.XInput; // Add this for GamepadButtonFlags
using XB2Midi.Models;

namespace XB2Midi.Views
{
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
            var border = FindName(name) as Border;
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
                Brushes.Gray;
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
            var thumb = FindName(name) as Canvas;
            if (thumb == null) return;

            if (value is { } pos)
            {
                // Calculate center position of container (100x100) minus half of thumb size (20x20)
                const double centerOffset = (100 - 20) / 2;
                
                // Convert XInput values (-32768 to 32767) to canvas coordinates
                double x = ((dynamic)pos).X / 32767.0 * 40; // Scale by 40 to keep within bounds
                double y = -((dynamic)pos).Y / 32767.0 * 40; // Negative Y for correct direction
                
                // Set position relative to center
                Canvas.SetLeft(thumb, centerOffset + x);
                Canvas.SetTop(thumb, centerOffset + y);
            }
        }
    }
}