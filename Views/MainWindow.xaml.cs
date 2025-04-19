using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Add this for ToggleButton
using System.Windows.Input;  // Add this for KeyEventArgs
using System.Linq;  // Add this for Cast<T>() extension method
using NAudio.Midi;
using System.Collections.ObjectModel;
using System.Threading.Tasks;  // Add this for Task
using XB2Midi.Models;
using System.Diagnostics;
using SharpDX.XInput; // Add this for GamepadButtonFlags
using System.Windows.Shapes; // Add this for Rectangle

namespace XB2Midi.Views
{
    public partial class MainWindow : Window
    {
        private XboxController? controller;
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private MappingManager? chordMappingManager;
        private MappingManager? arpeggioMappingManager;
        private MappingManager? multiMappingManager;
        private ObservableCollection<string> midiLog = new();
        private readonly TestControllerSimulator? testSimulator = null;
        private ModeState modeState = new ModeState();
        private ControllerVisualizer controllerVisualizer = new ControllerVisualizer();
        private MappingTabManager mappingTabManager = new MappingTabManager(); // Add this

        // Dictionary to store mode-specific tab headers
        private Dictionary<ControllerMode, List<string>> modeTabsRegistry = new Dictionary<ControllerMode, List<string>>();



        public MainWindow()
        {
            InitializeComponent();

            // Center the window on screen
            CenterWindowOnScreen();

            try
            {
                // Initialize tab headers with consistent layout
                InitializeTabHeaders();

                // Initialize the Tab Header Registry
                InitializeModeTabsRegistry();

                // Initialize test simulator
                testSimulator = new TestControllerSimulator();
                InitializeTestController(); // Make sure this is called

                // Initialize MIDI output
                midiOutput = new MidiOutput();

                // Initialize mapping manager
                mappingManager = new MappingManager(midiOutput);
                chordMappingManager = new MappingManager(midiOutput);
                arpeggioMappingManager = new MappingManager(midiOutput);
                multiMappingManager = new MappingManager(midiOutput);


                // Pass MIDI output to views
                if (BasicMappingView != null)
                {
                    BasicMappingView.Initialize(midiOutput, mappingManager);
                    //    BasicMappingView.SetMidiOutput(midiOutput);
                }

                // Pass MIDI output to ChordMappingView
                if (ChordMappingView != null)
                {
                    ChordMappingView.Initialize(midiOutput, chordMappingManager);
                }

                // Pass MIDI output to ArpeggioMappingView
                //ArpeggioMappingView?.SetMidiOutput(midiOutput);

                if (ArpeggioMappingView != null)
                {
                    ArpeggioMappingView.Initialize(midiOutput, arpeggioMappingManager);
                }

                // Pass MIDI output to MultiMappingView
                //MultiMappingView?.SetMidiOutput(midiOutput);

                if (MultiMappingView != null)
                {
                    MultiMappingView.Initialize(midiOutput, multiMappingManager);
                }

                // Initialize controller
                controller = new XboxController();
                controller.InputChanged += Controller_InputChanged;
                controller.ConnectionChanged += Controller_ConnectionChanged;

                // Make sure to explicitly check the controller connection status
                bool isControllerConnected = controller.IsConnected;

                // Update both the window title and status indicator with the correct state
                UpdateControllerStatus(isControllerConnected);
                UpdateControllerStatusIndicator(isControllerConnected);

                // Initialize UI elements
                PopulateMappingDevices();
                //PopulateControllerInputs();



                // Set up controller status updates
                UpdateControllerStatus(controller.IsConnected);

                // Set up initial mode display
                UpdateModeDisplay(modeState.CurrentMode);

                // Initialize chord mode UI for the original tab only
                //InitializeChordModeUI();

                // IMPORTANT: Connect test visualizer events when the control is loaded
                this.Loaded += (s, e) =>
                {
                    if (TestVisualizer is InteractiveControllerVisualizer interactiveVisualizer)
                    {
                        // Make sure we're not double-subscribing
                        interactiveVisualizer.SimulateInput -= TestVisualizer_SimulateInput;
                        interactiveVisualizer.SimulateInput += TestVisualizer_SimulateInput;
                        Debug.WriteLine("TestVisualizer SimulateInput event connected");
                    }
                };

                // Start the update loop
                CompositionTarget.Rendering += (s, e) => controller.Update();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}\n{ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Ensure the controller status is updated when the window is fully loaded
            this.Loaded += (s, e) =>
            {
                if (controller != null)
                {
                    // Force an update of the controller status after UI is fully loaded
                    bool isConnected = controller.IsConnected;
                    UpdateControllerStatus(isConnected);
                    UpdateControllerStatusIndicator(isConnected);
                    Debug.WriteLine($"Window Loaded: Controller connection status updated: {(isConnected ? "Connected" : "Disconnected")}");
                }
            };
        }

        // Add this new method to center the window on the screen
        private void CenterWindowOnScreen()
        {
            // Get the current screen dimensions
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Calculate the center position
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = (screenHeight - this.Height) / 2;

            // This ensures the window is positioned before showing it to the user
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }


        private void InitializeTestController()
        {
            testSimulator.SimulatedInput += (s, e) =>
            {
                // Process events during spring-back animation
                if (e.InputType == ControllerInputType.Thumbstick)
                {
                    // Extract X and Y values
                    dynamic stickValue = e.Value;
                    short xValue = stickValue.X;
                    short yValue = stickValue.Y;

                    // Check for X-axis mapping (for pitch bend)
                    string xAxisName = $"{e.InputName}X";
                    var xMapping = mappingManager?.GetControllerMapping(xAxisName);
                    if (xMapping != null && modeState.CurrentMode == ControllerMode.Basic)
                    {
                        Debug.WriteLine($"Spring-back: MIDI for {xAxisName} = {xValue}");
                        HandleMidiOutput(xMapping, xValue);
                    }

                    // Check for Y-axis mapping (for pitch bend)
                    string yAxisName = $"{e.InputName}Y";
                    var yMapping = mappingManager?.GetControllerMapping(yAxisName);
                    if (yMapping != null && modeState.CurrentMode == ControllerMode.Basic)
                    {
                        Debug.WriteLine($"Spring-back: MIDI for {yAxisName} = {yValue}");
                        HandleMidiOutput(yMapping, yValue);
                    }
                }

                // Update visualizer to match the simulated input
                TestVisualizer?.UpdateControl(e);

                Dispatcher.Invoke(() =>
                {
                    // Log all movements including spring-back
                    if (e.InputType == ControllerInputType.Thumbstick)
                    {
                        dynamic value = e.Value;
                        TestResultsLog.Items.Insert(0,
                            $"{DateTime.Now:HH:mm:ss.fff} - {e.InputName}: X={value.X}, Y={value.Y}");
                        if (TestResultsLog.Items.Count > 100)
                            TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                    }
                });
            };
        }

        private void Controller_InputChanged(object? sender, ControllerInputEventArgs e)
        {
            // Get the gamepad state once to avoid repeated calls
            var gamepadState = controller?.GetState()?.Gamepad;
            
            // Set shoulder button states in the event args
            if (gamepadState != null)
            {
                e.IsLeftShoulderPressed = gamepadState.Value.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
                e.IsRightShoulderPressed = gamepadState.Value.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
            }
            
            // Update the debug visualizer with controller input
            if (sender == controller)  // Only update visualizer for physical controller input
            {
                // Explicitly make sure visualization happens on the UI thread
                Dispatcher.Invoke(() =>
                {
                    // Update both visualizers
                    DebugVisualizer?.UpdateControl(e);
                    controllerVisualizer?.UpdateControl(e);

                    // Update last input indicator in visualizer tab
                    UpdateLastInputIndicator(e);

                    // Only log physical controller input in the debug tab if it's significant
                    if (e.InputType != ControllerInputType.Thumbstick || IsSignificantThumbstickMovement(e.Value))
                    {
                        InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputName}: {e.Value}");
                        while (InputLog.Items.Count > 100)
                            InputLog.Items.RemoveAt(InputLog.Items.Count - 1);
                    }
                });
            }

            // Track left joystick position for chord inversions - FIXED VERSION
            if ((e.InputName == "LeftThumbstick" || e.InputName == "LeftThumbstickX" || e.InputName == "LeftThumbstickY")
                && (e.InputType == ControllerInputType.Thumbstick))
            {
                if (modeState.CurrentMode == ControllerMode.Chord)
                {
                    short xValue = 0;
                    short yValue = 0;

                    try
                    {
                        // Extract X and Y based on input name and object type
                        if (e.InputName == "LeftThumbstick")
                        {
                            dynamic stickValue = e.Value;
                            xValue = stickValue.X;
                            yValue = stickValue.Y;
                        }
                        else if (e.InputName == "LeftThumbstickX")
                        {
                            // Use safe dynamic access for this complex type
                            dynamic complexValue = e.Value;
                            if (complexValue != null && complexValue.GetType().GetProperty("X") != null)
                            {
                                xValue = complexValue.X;
                            }
                        }
                        else if (e.InputName == "LeftThumbstickY")
                        {
                            // Use safe dynamic access for this complex type
                            dynamic complexValue = e.Value;
                            if (complexValue != null && complexValue.GetType().GetProperty("Y") != null)
                            {
                                yValue = complexValue.Y;
                            }
                        }

                        // Update joystick position in mode state
                        modeState.UpdateLeftJoystickPosition(xValue, yValue);

                        int inversionLevel = modeState.GetCurrentInversion();
                    }
                    catch (Exception ex)
                    {
                        // Log the exception but don't crash
                        Debug.WriteLine($"Error extracting joystick values: {ex.Message}");
                    }
                }
            }

            // NEW CODE: Track left trigger value for velocity control in chord mode
            if (e.InputName == "LeftTrigger" && e.InputType == ControllerInputType.Trigger)
            {
                if (modeState.CurrentMode == ControllerMode.Chord)
                {
                    try
                    {
                        // Get trigger value (usually 0-255)
                        byte triggerValue = Convert.ToByte(e.Value);

                        // Update the trigger value in mode state
                        modeState.UpdateLeftTriggerValue(triggerValue);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing trigger value: {ex.Message}");
                    }
                }
            }

            // NEW CODE: Track right trigger value for sustain control in chord mode
            if (e.InputName == "RightTrigger" && e.InputType == ControllerInputType.Trigger)
            {
                if (modeState.CurrentMode == ControllerMode.Chord)
                {
                    try
                    {
                        // Get trigger value (usually 0-255)
                        byte triggerValue = Convert.ToByte(e.Value);

                        // Update the trigger value in mode state
                        modeState.UpdateRightTriggerValue(triggerValue);

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing right trigger value: {ex.Message}");
                    }
                }
            }

            // Handle mode switching
            if (e.InputType == ControllerInputType.Button)
            {

                bool backPressed = false;
                bool startPressed = false;

                // Handle different value types correctly - this might be the issue
                if (e.Value is int intValue)
                {
                    backPressed = e.InputName == "Back" && intValue != 0;
                    startPressed = e.InputName == "Start" && intValue != 0;
                }
                else if (e.Value is bool boolValue)
                {
                    backPressed = e.InputName == "Back" && boolValue;
                    startPressed = e.InputName == "Start" && boolValue;
                }
                else
                {
                    // For any other type, try converting to bool
                    backPressed = e.InputName == "Back" && Convert.ToBoolean(e.Value);
                    startPressed = e.InputName == "Start" && Convert.ToBoolean(e.Value);
                }

                bool modeChanged = modeState.HandleModeChange(backPressed, startPressed);

                if (modeChanged)
                {
                    // Update UI to reflect the new mode
                    UpdateModeDisplay(modeState.CurrentMode);
                    return;
                }
            }

            // Check if we should handle this input as MIDI
            if (!modeState.ShouldHandleAsMidiControl(e.InputName))
                return;

            // Handle input according to current mode
            switch (modeState.CurrentMode)
            {
                case ControllerMode.Chord:
                    // Always route to ChordMappingView if available
                    if (ChordMappingView != null)
                    {
                        ChordMappingView.HandleControllerInput(e);
                        return;
                    }
                    // In Chord mode, silently ignore non-button inputs (triggers, thumbsticks)
                    return;

                case ControllerMode.Basic:
                    // Always route to BasicMappingView if available
                    if (BasicMappingView != null)
                    {
                        BasicMappingView.HandleControllerInput(e);
                    }
                    break;

                case ControllerMode.Arpeggio:
                    // Arpeggio mode handling will be added later
                    // For now, silently ignore all inputs
                    return;

                case ControllerMode.Multi: // Was ControllerMode.Direct
                    // Delegate to MultiMappingView
                    MultiMappingView?.HandleControllerInput(e);
                    return;
            }
        }

        private void UpdateLastInputIndicator(ControllerInputEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var lastInputText = FindName("LastControllerInputText") as TextBlock;
                if (lastInputText != null)
                {
                    // Format the display based on input type
                    string displayValue;
                    if (e.InputType == ControllerInputType.Button)
                    {
                        bool isPressed = Convert.ToBoolean(e.Value);
                        displayValue = $"{e.InputName} {(isPressed ? "Pressed" : "Released")}";

                        // Show button press overlay for pressed buttons
                        if (isPressed)
                        {
                            ShowButtonPressOverlay(e.InputName);
                        }
                    }
                    else if (e.InputType == ControllerInputType.Trigger)
                    {
                        byte value = Convert.ToByte(e.Value);
                        displayValue = $"{e.InputName}: {value}/255";
                    }
                    else if (e.InputType == ControllerInputType.Thumbstick)
                    {
                        dynamic stick = e.Value;
                        displayValue = $"{e.InputName}: X={stick.X}, Y={stick.Y}";
                    }
                    else
                    {
                        displayValue = $"{e.InputName}: {e.Value}";
                    }

                    lastInputText.Text = displayValue;

                    // Also add to the visualizer activity log
                    var visualizerLog = FindName("VisualizerActivityLog") as ListBox;
                    if (visualizerLog != null &&
                        (e.InputType != ControllerInputType.Thumbstick || IsSignificantThumbstickMovement(e.Value)))
                    {
                        visualizerLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {displayValue}");
                        while (visualizerLog.Items.Count > 100)
                            visualizerLog.Items.RemoveAt(visualizerLog.Items.Count - 1);
                    }
                }

                // Update controller status indicator
                UpdateControllerStatusIndicator(controller?.IsConnected ?? false);
            });
        }

        private void ShowButtonPressOverlay(string buttonName)
        {
            // Skip this for thumbstick movements
            if (buttonName.Contains("Thumbstick"))
                return;

            var overlay = FindName("LastPressedButtonOverlay") as Border;
            var buttonText = FindName("LastPressedButtonText") as TextBlock;

            if (overlay != null && buttonText != null)
            {
                // Clean up the button name for display
                string displayName = buttonName
                    .Replace("Button", "")
                    .Replace("DPad", "D-")  // Make D-Pad buttons more readable
                    .Replace("Bumper", "B"); // Abbreviate Bumper to B

                // Set the button name
                buttonText.Text = displayName;

                // Show the overlay
                overlay.Visibility = Visibility.Visible;

                // Use a fade-out animation
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Tick += (s, e) =>
                {
                    overlay.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Interval = TimeSpan.FromMilliseconds(500);
                timer.Start();
            }
        }

        private void UpdateControllerStatusIndicator(bool isConnected)
        {
            var indicator = FindName("ControllerStatusIndicator") as Ellipse;
            var statusText = FindName("ControllerStatusText") as TextBlock;

            if (indicator != null && statusText != null)
            {
                if (isConnected)
                {
                    indicator.Fill = Brushes.LimeGreen;
                    statusText.Text = "Connected";
                }
                else
                {
                    indicator.Fill = Brushes.Red;
                    statusText.Text = "Disconnected";
                }
            }
        }


        private void HandleMidiOutput(MidiMapping mapping, object value)
        {
            if (midiOutput == null) return;

            switch (mapping.MessageType)
            {
                case MidiMessageType.Note:
                    bool isPressed = Convert.ToBoolean(value);
                    if (isPressed)
                    {
                        midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, 127);
                        LogMidiEvent($"Note On: {mapping.NoteNumber} on channel {mapping.Channel}");
                    }
                    else
                    {
                        midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber);
                        LogMidiEvent($"Note Off: {mapping.NoteNumber} on channel {mapping.Channel}");
                    }
                    break;

                case MidiMessageType.ControlChange:
                    byte controlValue = Convert.ToByte(value);
                    midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, mapping.ControllerNumber, controlValue);
                    LogMidiEvent($"Control Change: {mapping.ControllerNumber} = {controlValue}");
                    break;

                case MidiMessageType.PitchBend:
                    // Convert value to pitch bend range (0-16383)
                    short pitchValue;

                    if (value is short shortValue)
                    {
                        // Map from -32768 to 32767 to 0 to 16383
                        pitchValue = (short)((shortValue + 32768) / 4);
                    }
                    else
                    {
                        try
                        {
                            // Try to extract X value using dynamic
                            dynamic dynamicValue = value;
                            if (dynamicValue != null)
                            {
                                // Use reflection to check for X property instead of LINQ
                                var properties = dynamicValue.GetType().GetProperties();
                                bool hasXProperty = false;
                                foreach (var prop in properties)
                                {
                                    if (prop.Name == "X")
                                    {
                                        hasXProperty = true;
                                        break;
                                    }
                                }

                                if (hasXProperty)
                                {
                                    short xValue = Convert.ToInt16(dynamicValue.X);
                                    pitchValue = (short)((xValue + 32768) / 4);
                                    // Debug.WriteLine($"Extracting X value for pitch bend: {xValue} -> {pitchValue}");
                                }
                                else
                                {
                                    // Try to convert other types
                                    pitchValue = Convert.ToInt16(value);
                                }
                            }
                            else
                            {
                                // Try to convert other types
                                pitchValue = Convert.ToInt16(value);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error converting pitch bend value: {ex.Message}");
                            // Default to center value if conversion fails
                            pitchValue = 8192;
                        }
                    }

                    // Ensure value is in range
                    pitchValue = (short)Math.Clamp((int)pitchValue, 0, 16383);

                    midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchValue);
                    LogMidiEvent($"Pitch Bend: {pitchValue}");
                    break;
            }
        }

        // Careful it might be called in more than one file
        private bool IsSignificantThumbstickMovement(object value)
        {
            try
            {
                dynamic stick = value;
                short x = Convert.ToInt16(stick.X);
                short y = Convert.ToInt16(stick.Y);

                return Math.Abs(x) > 1000 || Math.Abs(y) > 1000;
            }
            catch
            {
                return false;
            }
        }


        // Careful it might be called in more than one file
        private void HandleButtonMidi(string button, object value)
        {
            if (midiOutput == null) return;
            bool isPressed = Convert.ToInt32(value) != 0;

            var args = new ControllerInputEventArgs(
                ControllerInputType.Button,
                button,
                isPressed ? 127 : 0
            );

            // Fix null reference warning with null conditional operator
            mappingManager?.HandleControllerInput(args);

            if (isPressed)
            {
                LogMidiEvent($"Button {button} triggered");
            }
        }


        // Calls midi manager
        // Careful it might be called in more than one file
        private void HandleTriggerMidi(string trigger, object value)
        {
            if (midiOutput == null) return;
            byte controlValue = Convert.ToByte(value);

            var args = new ControllerInputEventArgs(
                ControllerInputType.Trigger,
                trigger,
                controlValue
            );

            mappingManager?.HandleControllerInput(args);
            LogMidiEvent($"Trigger {trigger}: {controlValue}");
        }


        // Careful this might be use din more than one place
        private void HandleThumbstickMidi(string stick, object value)
        {
            if (midiOutput == null) return;

            var args = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                stick,
                value
            );

            // Fix null reference warning with null conditional operator
            mappingManager?.HandleControllerInput(args);

            LogMidiEvent($"Stick {stick}: X={((dynamic)value).X}, Y={((dynamic)value).Y}");
        }


        // Basic Mode UI clear log
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            // Find the nearest ListBox to clear based on which button was clicked
            if (sender is FrameworkElement element)
            {
                // Try to find VisualizerActivityLog in the visual tree of the clicked button
                var visualizerLog = FindVisualParent<DockPanel>(element)?.FindName("VisualizerActivityLog") as ListBox;

                if (visualizerLog != null)
                {
                    // If found, clear the visualizer log
                    visualizerLog.Items.Clear();
                }
                else
                {
                    // Default to clearing the main InputLog
                    InputLog.Items.Clear();
                }
            }
            else
            {
                // Fallback to clearing the main InputLog
                InputLog.Items.Clear();
            }
        }

        // Helper method to find a parent of a specific type in the visual tree
        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            // Get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            // We've reached the end of the tree
            if (parentObject == null) return null;

            // Check if the parent matches the type we're looking for
            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                // Use recursion to proceed with the next level
                return FindVisualParent<T>(parentObject);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            controller?.Dispose();
            midiOutput?.Dispose();
            base.OnClosed(EventArgs.Empty);
        }

        private void RefreshMidiDevices()
        {
            PopulateMappingDevices();
        }

        // Refresh Devices Button on Basic Mode 
        private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            PopulateMappingDevices();
        }

        private void LogMidiEvent(string message)
        {
            // Add to in-memory log
            midiLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");

            // Update UI if available
            var midiActivityLog = this.FindName("MidiActivityLog") as ListBox;
            if (midiActivityLog != null)
            {
                Dispatcher.Invoke(() =>
                {
                    // Ensure we don't keep an unlimited log in memory
                    while (midiLog.Count > 100)
                        midiLog.RemoveAt(midiLog.Count - 1);

                    midiActivityLog.ItemsSource = null;
                    midiActivityLog.ItemsSource = midiLog;
                });
            }

            Debug.WriteLine($"MIDI: {message}");
        }
        private void SendMidiMessage(int deviceIndex, int channel, int noteNumber, int velocity)
        {
            if (midiOutput == null) return;

            try
            {
                // Add the missing cast for deviceIndex
                midiOutput.SendNoteOn((byte)deviceIndex, (byte)channel, (byte)noteNumber, (byte)velocity);
                LogMidiEvent($"Note On - Device: {deviceIndex}, Channel: {channel}, Note: {noteNumber}, Velocity: {velocity}");
            }
            catch (Exception ex)
            {
                LogMidiEvent($"Error sending MIDI: {ex.Message}");
            }
        }

        private void Controller_ConnectionChanged(object? sender, bool isConnected)
        {

            // Update controller status indicator
            UpdateControllerStatusIndicator(isConnected);

            // If needed, update window title or log the event
            LogMidiEvent($"Controller {(isConnected ? "connected" : "disconnected")}");
        }
        private void UpdateControllerStatus(bool isConnected)
        {
            // Optionally update the window title to show controller status
            Dispatcher.Invoke(() =>
            {
                string currentTitle = this.Title;
                if (currentTitle.Contains(" - "))
                {
                    currentTitle = currentTitle.Substring(0, currentTitle.IndexOf(" - "));
                }
                this.Title = $"{currentTitle} - Controller {(isConnected ? "Connected" : "Disconnected")}";
            });
        }

        // I belive this has to do with the Controller Simulator Tab
        private void TestVisualizer_SimulateInput(object? sender, ControllerInputEventArgs e)
        {
            if (e.InputType == ControllerInputType.ThumbstickRelease)
            {
                // Handle thumbstick release as before
                dynamic value = e.Value;
                Point releasePos = value.ReleasePosition;
                testSimulator?.SimulateStickRelease(e.InputName, releasePos);

                // Log the event
                Dispatcher.Invoke(() =>
                {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - Release: {e.InputName} from X={releasePos.X:F2}, Y={releasePos.Y:F2}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            else if (e.InputType == ControllerInputType.Thumbstick)
            {
                // Process thumbstick inputs - need to convert from combined to individual axis format
                dynamic stickValue = e.Value;
                short xValue = stickValue.X;
                short yValue = stickValue.Y;

                // Check for X-axis mapping
                string xAxisName = $"{e.InputName}X";
                var xMapping = mappingManager?.GetControllerMapping(xAxisName);
                if (xMapping != null && modeState.CurrentMode == ControllerMode.Basic)
                {
                    HandleMidiOutput(xMapping, xValue);
                }

                // Check for Y-axis mapping
                string yAxisName = $"{e.InputName}Y";
                var yMapping = mappingManager?.GetControllerMapping(yAxisName);
                if (yMapping != null && modeState.CurrentMode == ControllerMode.Basic)
                {
                    HandleMidiOutput(yMapping, yValue);
                }

                // Also pass to regular path for other processing
                Controller_InputChanged(testSimulator, e);

                // Log to the test results
                Dispatcher.Invoke(() =>
                {
                    TestResultsLog.Items.Insert(0,
                        $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} X={xValue}, Y={yValue}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            else if (e.InputType == ControllerInputType.Button)
            {
                // Directly process button mappings
                var mapping = mappingManager?.GetControllerMapping(e.InputName);
                if (mapping != null && modeState.CurrentMode == ControllerMode.Basic)
                {
                    // Convert bool to appropriate value
                    bool isPressed = Convert.ToBoolean(e.Value);

                    // Process the mapping directly
                    HandleMidiOutput(mapping, isPressed);
                }
                else
                {
                    // Still try the regular path as fallback
                    Controller_InputChanged(testSimulator, e);
                }

                // Log to the test results
                Dispatcher.Invoke(() =>
                {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            else
            {
                // Handle other input types (like triggers)
                Controller_InputChanged(testSimulator, e);

                // Log to the test results
                Dispatcher.Invoke(() =>
                {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }

            // Always update the visualizer
            TestVisualizer?.UpdateControl(e);
        }

        // I htink this has to do with the Controller Simulator tab's controller and how fast the trigger activates when clicked on
        private void TriggerRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TestVisualizer != null)
            {
                TestVisualizer.TriggerRate = e.NewValue;
            }
        }

        // THis seems to have to do with the Controller Simulator on the Controller Simulator tab
        private void HandleTestSimulatedInput(object? sender, ControllerInputEventArgs e)
        {
            mappingManager?.HandleControllerInput(e);
            TestVisualizer?.UpdateControl(e);
        }

        // This seems to have to do with the joystick springback functionality exclusive to the Controller Simulator tab controller
        private void TestThumbstick_Released(string thumbstickName, Point lastPosition)
        {
            testSimulator.SimulateStickRelease(thumbstickName, lastPosition);
        }

        // This has to do with the joystick springback functionality exclusive to the Controller Simulator tab controller
        private void SpringBackRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (testSimulator != null)
            {
                testSimulator.SpringBackRate = e.NewValue;
            }
        }

        private void InputLog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var selectedItems = InputLog.Items.Cast<string>()
                    .Where(item => InputLog.SelectedItems.Contains(item))
                    .ToList();

                if (selectedItems.Any())
                {
                    Clipboard.SetText(string.Join(Environment.NewLine, selectedItems));
                }
            }
        }


       
        // Update UpdateModeDisplay to work with the registry
        private void UpdateModeDisplay(ControllerMode mode)
        {
            Dispatcher.Invoke(() =>
            {
                // Update window title
                this.Title = $"XB2MIDI - {mode} Mode";

                // Update test mode display (in Visualizer tab)
                var testModeDisplay = this.FindName("TestModeDisplay") as TextBlock;
                if (testModeDisplay != null)
                    testModeDisplay.Text = $"Mode: {mode}";

                // Update visualizers - checking for null first
                controllerVisualizer?.UpdateModeLEDs(mode);

                // Update visualizer controls as before
                if (this.FindName("DebugVisualizer") is BaseControllerVisualizer debugVisualizer)
                {
                    debugVisualizer.UpdateModeLEDs(mode);
                }

                if (this.FindName("TestVisualizer") is BaseControllerVisualizer testVisualizer)
                {
                    testVisualizer.UpdateModeLEDs(mode);
                }

                // Clear indicators for ALL mode tabs
                foreach (var modeEntry in modeTabsRegistry)
                {
                    foreach (string tabName in modeEntry.Value)
                    {
                        var tab = this.FindName(tabName) as TabItem;
                        if (tab != null)
                        {
                            ClearModeIndicator(tab);
                        }
                    }
                }

                // Set indicators for the active mode tabs
                if (modeTabsRegistry.TryGetValue(mode, out var tabNames))
                {
                    Color modeColor = GetModeColor(mode);
                    foreach (string tabName in tabNames)
                    {
                        var tab = this.FindName(tabName) as TabItem;
                        if (tab != null)
                        {
                            SetModeIndicator(tab, modeColor);
                        }
                    }
                }

                // Log the mode change
                LogMidiEvent($"Mode changed to: {mode}");
            });
        }

        // Initialize the registry in the constructor or a separate initialization method
        private void InitializeModeTabsRegistry()
        {
            // Register each mode with its corresponding tabs
            modeTabsRegistry[ControllerMode.Basic] = new List<string> { "BasicMappingTab2" };
            modeTabsRegistry[ControllerMode.Chord] = new List<string> { "ChordMappingTab2" };
            modeTabsRegistry[ControllerMode.Arpeggio] = new List<string> { "ArpeggioMappingTab" };
            modeTabsRegistry[ControllerMode.Multi] = new List<string> { "MultiMappingTab" };
        }

        // Helper methods to set and clear mode indicators on tab headers
        // The mode indicator is the square that turns a color rather than transparent to indicate the mode we are on
        private void SetModeIndicator(TabItem? tab, Color color)
        {
            if (tab == null) return;

            // Get the existing header content
            if (tab.Header is string headerText)
            {
                // Create a new header with a consistent layout
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(5, 2, 5, 2) // Consistent padding for all tabs
                };

                // Add the text first
                stackPanel.Children.Add(new TextBlock
                {
                    Text = headerText,
                    VerticalAlignment = VerticalAlignment.Center
                });

                // Add the colored rectangle indicator after the text
                var indicator = new Rectangle
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(color),
                    Margin = new Thickness(5, 0, 0, 0), // Left margin instead of right
                    VerticalAlignment = VerticalAlignment.Center
                };

                stackPanel.Children.Add(indicator);

                // Replace the header
                tab.Header = stackPanel;
            }
            else if (tab.Header is StackPanel existingPanel)
            {
                // If we already have a StackPanel, just update the indicator color
                if (existingPanel.Children.Count > 1 && existingPanel.Children[1] is Rectangle rect)
                {
                    rect.Fill = new SolidColorBrush(color);
                }
            }
        }

        // This has to do with the mode indicator that appears on the mode tabs at the top of the program ui
        // When a mode is selected it has a color square indicator, however when its not selected it has a transparent indicator to preserve its tab width
        private void ClearModeIndicator(TabItem? tab)
        {
            if (tab == null) return;

            if (tab.Header is string headerText)
            {
                // Create a new header with placeholder for the indicator to maintain consistent width
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(5, 2, 5, 2) // Consistent padding
                };

                // Add the text first
                stackPanel.Children.Add(new TextBlock
                {
                    Text = headerText,
                    VerticalAlignment = VerticalAlignment.Center
                });

                // Add a transparent rectangle after the text to maintain space
                var placeholder = new Rectangle
                {
                    Width = 12,
                    Height = 12,
                    Fill = Brushes.Transparent,
                    Margin = new Thickness(5, 0, 0, 0), // Left margin instead of right
                    VerticalAlignment = VerticalAlignment.Center
                };

                stackPanel.Children.Add(placeholder);

                // Replace the header
                tab.Header = stackPanel;
            }
            else if (tab.Header is StackPanel existingPanel)
            {
                // If we already have a StackPanel, just make the indicator transparent
                if (existingPanel.Children.Count > 1 && existingPanel.Children[1] is Rectangle rect)
                {
                    rect.Fill = Brushes.Transparent;
                }
            }
        }

        // Update InitializeTabHeaders to handle all tabs in the registry
        private void InitializeTabHeaders()
        {
            // Process all tabs in the registry
            foreach (var modeEntry in modeTabsRegistry)
            {
                foreach (string tabName in modeEntry.Value)
                {
                    var tab = this.FindName(tabName) as TabItem;
                    if (tab != null)
                    {
                        // Extract the simple name (e.g., "BasicMappingTab" -> "Basic")
                        string simpleTitle = GetSimpleTitle(tabName);
                        tab.Header = simpleTitle;

                        // Initialize with transparent indicator
                        ClearModeIndicator(tab);
                    }
                }
            }
        }


        // Get appropriate color for each mode
        private Color GetModeColor(ControllerMode mode)
        {
            return mode switch
            {
                ControllerMode.Basic => Colors.DodgerBlue,
                ControllerMode.Chord => Colors.LimeGreen,
                ControllerMode.Arpeggio => Colors.Purple,
                ControllerMode.Multi => Colors.Orange,
                _ => Colors.Gray
            };
        }

        // Helper to get simple title from tab name
        private string GetSimpleTitle(string tabName)
        {
            if (tabName.EndsWith("MappingTab2"))
                return tabName.Replace("MappingTab2", "2");
            else if (tabName.EndsWith("MappingTab"))
                return tabName.Replace("MappingTab", "");

            return tabName;
        }

        private void PopulateMappingDevices()
        {
            var deviceList = new List<string>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                deviceList.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
            }
        }

  


   
    }
}
