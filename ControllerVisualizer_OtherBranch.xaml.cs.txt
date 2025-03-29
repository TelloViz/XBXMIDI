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

        // Add these fields at the top of the ControllerVisualizer class
        private Border? draggedThumbstick;
        private Point dragStart;
        private Canvas? dragCanvas;

        // Add these fields at the top of the class
        private ProgressBar? activeTrigger;
        private System.Windows.Threading.DispatcherTimer? triggerTimer;
        private double TRIGGER_RATE = 5.0; // Change in value per timer tick (adjustable)
        private const int TIMER_INTERVAL_MS = 16; // ~60Hz update rate
        private double lastTriggerValue = -1; // Add this field at class level

        public double TriggerRate
        {
            get { return (double)GetValue(TriggerRateProperty); }
            set { SetValue(TriggerRateProperty, value); }
        }

        public static readonly DependencyProperty TriggerRateProperty =
            DependencyProperty.Register("TriggerRate", typeof(double), typeof(ControllerVisualizer), 
                new PropertyMetadata(5.0, OnTriggerRateChanged));

        private static void OnTriggerRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var visualizer = (ControllerVisualizer)d;
            visualizer.TRIGGER_RATE = (double)e.NewValue;
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

                // Set up thumbstick drag events
                SetupThumbstickEvents("LeftThumbstick");
                SetupThumbstickEvents("RightThumbstick");

                // Set up trigger events
                SetupTriggerEvents("LeftTrigger");
                SetupTriggerEvents("RightTrigger");

                // Initialize trigger timer
                triggerTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
                };
                triggerTimer.Tick += TriggerTimer_Tick;
            }
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

            thumb.MouseLeftButtonUp += (s, e) =>
            {
                if (draggedThumbstick == thumb)
                {
                    // Get final position before release
                    Point currentPos = e.GetPosition(canvas);
                    
                    // Calculate normalized coordinates (-1 to 1 range)
                    double centerX = canvas.ActualWidth / 2;
                    double centerY = canvas.ActualHeight / 2;
                    double normalizedX = (currentPos.X - centerX) / 35.0; // Using 35 as the max range
                    double normalizedY = (currentPos.Y - centerY) / 35.0;
                    
                    // Create release position
                    Point releasePos = new Point(normalizedX, normalizedY);
                    
                    // Trigger spring-back simulation
                    SimulateInput?.Invoke(this, new ControllerInputEventArgs(
                        ControllerInputType.ThumbstickRelease,
                        thumbstickName,
                        new { ReleasePosition = releasePos }
                    ));
                    
                    draggedThumbstick = null;
                    thumb.ReleaseMouseCapture();
                }
            };

            thumb.MouseMove += (s, e) =>
            {
                if (draggedThumbstick == thumb && e.LeftButton == MouseButtonState.Pressed)
                {
                    Point currentPos = e.GetPosition(canvas);
                    HandleThumbstickDrag(thumbstickName, currentPos);
                }
            };

            // Handle right-click for L3/R3
            thumb.MouseRightButtonDown += (s, e) =>
            {
                SimulateThumbClick(thumbstickName, true);
                e.Handled = true;
            };

            thumb.MouseRightButtonUp += (s, e) =>
            {
                SimulateThumbClick(thumbstickName, false);
                e.Handled = true;
            };
        }

        private void HandleThumbstickDrag(string thumbstickName, Point currentPos)
        {
            if (dragCanvas == null) return;

            // Calculate center of the canvas
            double centerX = dragCanvas.ActualWidth / 2;
            double centerY = dragCanvas.ActualHeight / 2;

            // Calculate offset from center
            double deltaX = currentPos.X - centerX;
            double deltaY = currentPos.Y - centerY;

            // Calculate magnitude of the vector
            double magnitude = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            // Maximum allowed radius (35 units from center)
            const double MAX_RADIUS = 35.0;

            // If magnitude exceeds the maximum radius, normalize the vector
            if (magnitude > MAX_RADIUS)
            {
                deltaX = (deltaX / magnitude) * MAX_RADIUS;
                deltaY = (deltaY / magnitude) * MAX_RADIUS;
            }

            // Update visual position (centered on the thumb)
            Canvas.SetLeft(draggedThumbstick, centerX + deltaX - draggedThumbstick.ActualWidth / 2);
            Canvas.SetTop(draggedThumbstick, centerY + deltaY - draggedThumbstick.ActualHeight / 2);

            // Convert to normalized -1 to 1 range
            double normalizedX = deltaX / MAX_RADIUS;
            double normalizedY = deltaY / MAX_RADIUS;

            // Convert to XInput range (-32768 to 32767)
            short xValue = (short)(normalizedX * 32767);
            short yValue = (short)(-normalizedY * 32767); // Invert Y axis

            // Create input event
            var args = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                thumbstickName,
                new { X = xValue, Y = yValue, Pressed = false }
            );

            SimulateInput?.Invoke(this, args);
        }

        private void SimulateThumbClick(string thumbstickName, bool isPressed)
        {
            string buttonName = thumbstickName == "LeftThumbstick" ? "LeftThumbClick" : "RightThumbClick";
            var args = new ControllerInputEventArgs(
                ControllerInputType.Button,
                buttonName,
                isPressed ? 1 : 0
            );
            SimulateInput?.Invoke(this, args);
            UpdateThumbstick(thumbstickName, new { X = 0, Y = 0, Pressed = isPressed });
        }

        private void ResetThumbstick(string thumbstickName)
        {
            // Split into X and Y events to match real controller behavior
            string baseStickName = thumbstickName.Replace("Thumbstick", "");
            
            // Send X axis reset
            var xArgs = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                $"{baseStickName}ThumbstickX",
                new { X = (short)0, Y = (short)0, Pressed = false }
            );
            SimulateInput?.Invoke(this, xArgs);

            // Send Y axis reset
            var yArgs = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                $"{baseStickName}ThumbstickY",
                new { X = (short)0, Y = (short)0, Pressed = false }
            );
            SimulateInput?.Invoke(this, yArgs);

            // Reset visual position
            UpdateThumbstick(thumbstickName, new { X = 0, Y = 0, Pressed = false });
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

            // Make the trigger clickable
            trigger.Cursor = Cursors.Hand;
        }

        private void TriggerTimer_Tick(object? sender, EventArgs e)
        {
            if (activeTrigger == null) return;

            string triggerName = activeTrigger.Name.Replace("Value", "");
            double newValue = Math.Min(activeTrigger.Value + TRIGGER_RATE, 100);
            
            // Only update and send if value has changed
            if (Math.Abs(newValue - lastTriggerValue) > 0.01) // Small threshold for floating point comparison
            {
                activeTrigger.Value = newValue;
                lastTriggerValue = newValue;
                
                // Map 0-100 to 0-255 for regular triggers
                byte mappedValue = (byte)(newValue * 255 / 100);

                var args = new ControllerInputEventArgs(
                    ControllerInputType.Trigger,
                    triggerName,
                    mappedValue
                );
                SimulateInput?.Invoke(this, args);
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
                double newValue = Math.Max(trigger.Value - TRIGGER_RATE, 0);
                
                // Only update and send if value has changed
                if (Math.Abs(newValue - lastTriggerValue) > 0.01)
                {
                    trigger.Value = newValue;
                    lastTriggerValue = newValue;
                    
                    // Map 0-100 to 0-255 for regular triggers
                    byte mappedValue = (byte)(newValue * 255 / 100);

                    var args = new ControllerInputEventArgs(
                        ControllerInputType.Trigger,
                        triggerName,
                        mappedValue
                    );
                    SimulateInput?.Invoke(this, args);
                }

                if (newValue <= 0)
                {
                    releaseTimer.Stop();
                    lastTriggerValue = -1; // Reset for next trigger use
                }
            };

            releaseTimer.Start();
        }

        private void UpdateThumbstickPosition(Point mousePosition)
        {
            if (!IsInteractive || draggedThumbstick == null || dragCanvas == null) return;

            // Get canvas center and calculate deltas
            double centerX = dragCanvas.ActualWidth / 2;
            double centerY = dragCanvas.ActualHeight / 2;
            
            // Calculate vector from center
            double deltaX = mousePosition.X - centerX;
            double deltaY = mousePosition.Y - centerY;

            // Calculate magnitude
            double magnitude = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            // Maximum radius allowed (radius of the circular movement)
            double maxRadius = Math.Min(dragCanvas.ActualWidth, dragCanvas.ActualHeight) / 2;

            // If magnitude exceeds the maximum radius, normalize the vector
            if (magnitude > maxRadius)
            {
                deltaX = (deltaX / magnitude) * maxRadius;
                deltaY = (deltaY / magnitude) * maxRadius;
            }

            // Calculate final position
            double newX = centerX + deltaX;
            double newY = centerY + deltaY;

            // Update visual position
            Canvas.SetLeft(draggedThumbstick, newX - draggedThumbstick.ActualWidth / 2);
            Canvas.SetTop(draggedThumbstick, newY - draggedThumbstick.ActualHeight / 2);

            // Convert to normalized -1 to 1 range
            double normalizedX = deltaX / maxRadius;
            double normalizedY = deltaY / maxRadius;

            // Convert to controller range and fire event
            SimulateInput?.Invoke(this, new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                draggedThumbstick.Name,
                new { 
                    X = (short)(normalizedX * 32767),
                    Y = (short)(normalizedY * -32767),
                    Pressed = true 
                }
            ));
        }

        private void ThumbstickCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedThumbstick != null && dragCanvas != null)
            {
                Point currentPos = e.GetPosition(dragCanvas);
                UpdateThumbstickPosition(currentPos);
            }
        }
    }
}