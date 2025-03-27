using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input; 
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

        public bool IsInteractive
        {
            get { return (bool)GetValue(IsInteractiveProperty); }
            set { SetValue(IsInteractiveProperty, value); }
        }

        public static readonly DependencyProperty IsInteractiveProperty =
            DependencyProperty.Register("IsInteractive", typeof(bool), typeof(ControllerVisualizer), 
                new PropertyMetadata(false, OnIsInteractiveChanged));

        private static void OnIsInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var visualizer = (ControllerVisualizer)d;
            visualizer.SetupInteractivity((bool)e.NewValue);
        }

        private void SetupInteractivity(bool isInteractive)
        {
            if (isInteractive)
            {
                // Add click handlers to all interactive elements
                foreach (var element in GetInteractiveElements())
                {
                    if (element is Border border)
                    {
                        border.MouseDown += Border_MouseDown;
                        border.MouseUp += Border_MouseUp;
                        border.Cursor = Cursors.Hand;
                    }
                }
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractive || sender is not Border border) return;

            string controlName = border.Name;
            // Simulate controller input
            var args = new ControllerInputEventArgs(
                ControllerInputType.Button,
                controlName,
                1
            );
            SimulateInput?.Invoke(this, args);
            UpdateButton(controlName, 1);
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractive || sender is not Border border) return;

            string controlName = border.Name;
            var args = new ControllerInputEventArgs(
                ControllerInputType.Button,
                controlName,
                0
            );
            SimulateInput?.Invoke(this, args);
            UpdateButton(controlName, 0);
        }

        // Add event for simulated input
        public event EventHandler<ControllerInputEventArgs>? SimulateInput;

        private IEnumerable<FrameworkElement> GetInteractiveElements()
        {
            // Return all named Borders that represent buttons
            return new[] { "A", "B", "X", "Y", "LeftBumper", "RightBumper", 
                          "Back", "Start", "DPadUp", "DPadDown", "DPadLeft", "DPadRight" }
                .Select(name => FindName(name))
                .Where(element => element != null)
                .Cast<FrameworkElement>();
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