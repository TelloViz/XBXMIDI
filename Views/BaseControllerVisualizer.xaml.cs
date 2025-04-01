using System.Windows.Controls;
using System.Windows.Media;
using XB2Midi.Models;

public abstract class BaseControllerVisualizer : UserControl
{
    protected const double MAX_RADIUS = 35.0;
    protected const double CENTER_OFFSET = 35.0;

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