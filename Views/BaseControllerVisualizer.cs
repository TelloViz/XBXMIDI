using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XB2Midi.Models;

namespace XB2Midi.Views
{
    public abstract class BaseControllerVisualizer : UserControl
    {
        protected const double MAX_RADIUS = 35.0;
        protected const double CENTER_OFFSET = 35.0;

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

            button.Background = isPressed ? 
                Brushes.LightGreen : 
                (name.Contains("Thumbstick") ? Brushes.DarkGray : Brushes.Gray);
        }

        protected virtual void UpdateTriggerVisual(string name, byte value)
        {
            var trigger = FindName(name) as ProgressBar;
            if (trigger == null) return;

            trigger.Value = value;
        }
    }
}