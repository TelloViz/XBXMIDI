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
        private ObservableCollection<string> midiLog = new();
        private readonly TestControllerSimulator? testSimulator = null;
        private ModeState modeState = new ModeState();
        private ControllerVisualizer controllerVisualizer = new ControllerVisualizer();

        public MainWindow()
        {
            InitializeComponent();
            
            try
            {
                // Initialize tab headers with consistent layout
                InitializeTabHeaders();
                
                // Initialize test simulator
                testSimulator = new TestControllerSimulator();
                
                // Initialize MIDI output
                midiOutput = new MidiOutput();
                
                // Initialize controller
                controller = new XboxController();
                controller.InputChanged += Controller_InputChanged;
                controller.ConnectionChanged += Controller_ConnectionChanged;
                
                // Initialize UI elements
                PopulateMappingDevices();
                PopulateControllerInputs();
                
                // Initialize mapping manager
                mappingManager = new MappingManager(midiOutput);
                mappingManager.MappingsChanged += (s, e) => 
                {
                    // Update the mappings list view when mappings change
                    Dispatcher.Invoke(() => {
                        MappingsListView.ItemsSource = mappingManager.GetCurrentMappings();
                    });
                };
                
                // Set up controller status updates
                UpdateControllerStatus(controller.IsConnected);
                
                // Set up initial mode display
                UpdateModeDisplay(modeState.CurrentMode);
                
                // Initialize chord mode UI
                InitializeChordModeUI();
                
                // Start the update loop
                CompositionTarget.Rendering += (s, e) => controller.Update();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}\n{ex.StackTrace}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateControllerInputs()
        {
            var inputs = new List<string> {
                "A", "B", "X", "Y",
                "LeftBumper", "RightBumper",
                "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
                "LeftTrigger", "RightTrigger",
                "LeftThumbstickX", "LeftThumbstickY",
                "RightThumbstickX", "RightThumbstickY"
            };
            
            ControllerInputComboBox.Items.Clear();
            foreach (var input in inputs)
            {
                ControllerInputComboBox.Items.Add(new ComboBoxItem { Content = input });
            }
            
            if (ControllerInputComboBox.Items.Count > 0)
            {
                ControllerInputComboBox.SelectedIndex = 0;
            }
        }

        private void InitializeTestController()
        {
            testSimulator.SimulatedInput += (s, e) =>
            {
                // Handle all simulated input including spring-back
                mappingManager?.HandleControllerInput(e);
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
            // Add debug logging to see all button inputs
            Debug.WriteLine($"Controller input: {e.InputType} - {e.InputName} = {e.Value} ({e.Value.GetType().Name})");

            // Add this near the top of your Controller_InputChanged method
            if (e.InputName == "Start" || e.InputName == "Back")
            {
                Debug.WriteLine($"[DETAILED] Mode button: {e.InputName} = {e.Value} ({e.Value.GetType().Name}) from {sender?.GetType().Name}");
            }

            // Fix null reference warning with safe navigation operator
            controllerVisualizer?.UpdateControl(e);

            // Handle mode switching
            if (e.InputType == ControllerInputType.Button)
            {
                // Check exact values coming from physical vs. virtual controller
                if (e.InputName == "Start" || e.InputName == "Back") 
                {
                    Debug.WriteLine($"Mode button pressed: {e.InputName} = {e.Value} (Type: {e.Value.GetType().Name})");
                }
                
                bool backPressed = e.InputName == "Back" && Convert.ToBoolean(e.Value);
                bool startPressed = e.InputName == "Start" && Convert.ToBoolean(e.Value);
                
                Debug.WriteLine($"Mode check: Back={backPressed}, Start={startPressed}");
                
                bool modeChanged = modeState.HandleModeChange(backPressed, startPressed);
                Debug.WriteLine($"Mode changed: {modeChanged}, Current mode: {modeState.CurrentMode}");
                
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
                    if (e.InputType == ControllerInputType.Button)
                    {
                        var gamepadState = controller?.GetState()?.Gamepad;
                        bool leftBumperHeld = gamepadState?.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) ?? false;
                        bool rightBumperHeld = gamepadState?.Buttons.HasFlag(GamepadButtonFlags.RightShoulder) ?? false;

                        // Process button input through chord handling
                        bool inputHandled = modeState.HandleButtonInput(e.InputName, Convert.ToBoolean(e.Value), leftBumperHeld, rightBumperHeld);
                        
                        // In Chord mode, we ignore all basic mappings, whether the chord handling succeeded or not
                        return;
                    }
                    // In Chord mode, silently ignore non-button inputs (triggers, thumbsticks)
                    return;
                    
                case ControllerMode.Basic:
                    // In Basic mode, process all inputs through the mapping manager
                    if (mappingManager != null)
                    {
                        var mapping = mappingManager.GetControllerMapping(e.InputName);
                        if (mapping != null)
                        {
                            HandleMidiOutput(mapping, e.Value);
                        }
                    }
                    break;
                    
                case ControllerMode.Arpeggio:
                    // Arpeggio mode handling will be added later
                    // For now, silently ignore all inputs
                    return;
                    
                case ControllerMode.Direct:
                    // Direct mode handling will be added later
                    // For now, silently ignore all inputs
                    return;
            }
        }

        private void HandleChordOutput(byte rootNote, byte thirdNote, byte fifthNote, bool isOn)
        {
            if (midiOutput == null) return;

            byte velocity = isOn ? (byte)127 : (byte)0;
            byte channel = 0; // You might want to make this configurable

            // Fix these calls:
            midiOutput.SendNoteOn(0, channel, rootNote, velocity);
            midiOutput.SendNoteOn(0, channel, thirdNote, velocity);
            midiOutput.SendNoteOn(0, channel, fifthNote, velocity);
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
                        // Try to convert other types
                        pitchValue = Convert.ToInt16(value);
                    }
                    
                    // Ensure value is in range
                    pitchValue = (short)Math.Clamp((int)pitchValue, 0, 16383);
                    
                    midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchValue);
                    LogMidiEvent($"Pitch Bend: {pitchValue}");
                    break;
            }
        }

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

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            InputLog.Items.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            controller?.Dispose();
            midiOutput?.Dispose();
            base.OnClosed(e);
        }

        private void RefreshMidiDevices()
        {
            PopulateMappingDevices();
        }

        private void MidiDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

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
                Dispatcher.Invoke(() => {
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

        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ControllerInputComboBox.SelectedItem == null || 
                    MidiTypeComboBox.SelectedItem == null || 
                    BasicMappingDeviceComboBox.SelectedIndex < 0)  // Updated here
                {
                    MessageBox.Show("Please select controller input, MIDI message type, and MIDI device.", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }

                string controllerInput = (ControllerInputComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string deviceString = BasicMappingDeviceComboBox.SelectedItem.ToString() ?? "";  // Updated here
                int deviceIndex = int.Parse(deviceString.Split(':')[0]);
                
                if (!byte.TryParse(MidiChannelTextBox.Text, out byte channel) || channel < 1 || channel > 16)
                {
                    MessageBox.Show("Please enter a valid MIDI channel (1-16).", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }
                
                // Adjust channel to be 0-based for internal handling
                channel--;
                
                MidiMessageType messageType = midiType switch
                {
                    "Note" => MidiMessageType.Note,
                    "Control Change" => MidiMessageType.ControlChange,
                    "Pitch Bend" => MidiMessageType.PitchBend,
                    _ => MidiMessageType.ControlChange
                };

                var mapping = new MidiMapping
                {
                    ControllerInput = controllerInput.Replace(" Button", "").Replace(" ", ""),
                    MessageType = messageType,
                    Channel = channel,
                    MinValue = 0,
                    MaxValue = messageType == MidiMessageType.PitchBend ? 16383 : 127,
                    MidiDeviceIndex = deviceIndex,
                    MidiDeviceName = deviceString
                };

                if (messageType != MidiMessageType.PitchBend)
                {
                    if (!byte.TryParse(MidiValueTextBox.Text, out byte value) || value > 127)
                    {
                        MessageBox.Show("Please enter a valid value (0-127).", 
                                      "Validation Error", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Warning);
                        return;
                    }
                    
                    if (messageType == MidiMessageType.Note)
                    {
                        mapping.NoteNumber = value;
                    }
                    else
                    {
                        mapping.ControllerNumber = value;
                    }
                }

                mappingManager?.AddMapping(mapping);
                
                // Refresh the list view
                MappingsListView.ItemsSource = mappingManager?.GetCurrentMappings();

                LogMidiEvent($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType} on device {mapping.MidiDeviceName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding mapping: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void DeleteMapping_Click(object sender, RoutedEventArgs e)
        {
            if (MappingsListView.SelectedItem is MidiMapping selectedMapping && mappingManager != null)
            {
                mappingManager.RemoveMapping(selectedMapping);
                MappingsListView.ItemsSource = mappingManager.GetCurrentMappings();
                LogMidiEvent($"Removed mapping for {selectedMapping.ControllerInput}");
            }
        }

        private void Controller_ConnectionChanged(object? sender, bool isConnected)
        {
            // We're no longer updating a UI element here since ControllerStatus was removed
            Debug.WriteLine($"Controller connection changed: {(isConnected ? "Connected" : "Disconnected")}");
            
            // If needed, update window title or log the event
            LogMidiEvent($"Controller {(isConnected ? "connected" : "disconnected")}");
        }

        private void UpdateControllerStatus(bool isConnected)
        {
            // Since we removed the ControllerStatus TextBlock, we'll just log the status
            Debug.WriteLine($"Controller status: {(isConnected ? "Connected" : "Disconnected")}");
            
            // Optionally update the window title to show controller status
            Dispatcher.Invoke(() => {
                string currentTitle = this.Title;
                if (currentTitle.Contains(" - "))
                {
                    currentTitle = currentTitle.Substring(0, currentTitle.IndexOf(" - "));
                }
                this.Title = $"{currentTitle} - Controller {(isConnected ? "Connected" : "Disconnected")}";
            });
        }

        private void TestVisualizer_SimulateInput(object? sender, ControllerInputEventArgs e)
        {
            Debug.WriteLine($"TestVisualizer_SimulateInput: {e.InputType} - {e.InputName}");
            
            if (e.InputType == ControllerInputType.ThumbstickRelease)
            {
                dynamic value = e.Value;
                Point releasePos = value.ReleasePosition;
                Debug.WriteLine($"Starting spring-back: {e.InputName} at {releasePos}");
                testSimulator.SimulateStickRelease(e.InputName, releasePos);

                // Log the release event
                Dispatcher.Invoke(() => {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - Release: {e.InputName} from X={releasePos.X:F2}, Y={releasePos.Y:F2}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            else
            {
                Debug.WriteLine($"Handling regular input: {e.InputType} - {e.InputName} = {e.Value}");
                mappingManager?.HandleControllerInput(e);
                
                Dispatcher.Invoke(() => {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            
            // Always update the visualizer
            TestVisualizer?.UpdateControl(e);
        }

        private void TriggerRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TestVisualizer != null)
            {
                TestVisualizer.TriggerRate = e.NewValue;
            }
        }

        private void MidiTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MidiValueTextBox != null)
            {
                bool isPitchBend = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() == "Pitch Bend";
                MidiValueTextBox.IsEnabled = !isPitchBend;
                if (isPitchBend)
                {
                    MidiValueTextBox.Text = "";
                }
            }
        }

        private void TestChord_Click(object sender, RoutedEventArgs e)
        {
            // Play a C major chord as a test
            byte rootNote = 60; // C4
            byte thirdNote = (byte)(rootNote + 4); // E
            byte fifthNote = (byte)(rootNote + 7); // G
            
            if (midiOutput != null && BasicMappingDeviceComboBox.SelectedIndex >= 0)
            {
                string deviceString = BasicMappingDeviceComboBox.SelectedItem?.ToString() ?? "";
                if (int.TryParse(deviceString.Split(':')[0], out int deviceIndex))
                {
                    // Play the chord
                    midiOutput.SendNoteOn(deviceIndex, 0, rootNote, 100);
                    midiOutput.SendNoteOn(deviceIndex, 0, thirdNote, 100);
                    midiOutput.SendNoteOn(deviceIndex, 0, fifthNote, 100);
                    
                    // Schedule note-off after 500ms
                    Task.Delay(500).ContinueWith(_ => {
                        midiOutput.SendNoteOff(deviceIndex, 0, rootNote);
                        midiOutput.SendNoteOff(deviceIndex, 0, thirdNote);
                        midiOutput.SendNoteOff(deviceIndex, 0, fifthNote);
                    });
                    
                    LogMidiEvent($"Test chord played: C major (notes: {rootNote}, {thirdNote}, {fifthNote}) on device {deviceString}");
                }
            }
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
        }

        private void HandleTestSimulatedInput(object? sender, ControllerInputEventArgs e)
        {
            mappingManager?.HandleControllerInput(e);
            TestVisualizer?.UpdateControl(e);
        }

        private void TestThumbstick_Released(string thumbstickName, Point lastPosition)
        {
            testSimulator.SimulateStickRelease(thumbstickName, lastPosition);
        }

        private void SpringBackRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (testSimulator != null)
            {
                testSimulator.SpringBackRate = e.NewValue;
                System.Diagnostics.Debug.WriteLine($"Spring-back rate updated to: {e.NewValue:F3}");
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

        // Move MappingLog handling to MappingManager class
        private void MappingManager_MappingEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                midiLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(midiLog.Count - 1);
            });
        }

        private void SaveMappings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Save Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    mappingManager?.SaveMappings(dialog.FileName);
                    LogMidiEvent($"Mappings saved to {dialog.FileName}");
                    MessageBox.Show("Mappings saved successfully!", "Success", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving mappings: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadMappings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Load Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    mappingManager?.LoadMappings(dialog.FileName);
                    LogMidiEvent($"Mappings loaded from {dialog.FileName}");
                    MessageBox.Show("Mappings loaded successfully!", "Success", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading mappings: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Subscribe to chord mappings loaded event
            if (mappingManager != null)
            {
                mappingManager.ChordMappingsLoaded += (s, chordMapping) => {
                    if (modeState != null)
                    {
                        Dispatcher.Invoke(() => {
                            chordMapping.ApplyTo(modeState);
                            
                            // Update button note mapping combos
                            UpdateButtonNoteComboBoxes();
                            
                            // Also update channel and device combos
                            UpdateChannelAndDeviceSelectors();
                            
                            LogMidiEvent("Chord mappings loaded and applied");
                        });
                    }
                };
            }
        }

        private void LoadChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (mappingManager == null) return;
            
            try
            {
                // Ask user to select a file
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    Title = "Load Chord Mappings"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // Load mappings from file
                    mappingManager.LoadMappings(dialog.FileName);
                    
                    // Apply chord mappings to current state
                    if (mappingManager.LoadChordMapping(modeState))
                    {
                        // Update UI to reflect loaded settings
                        UpdateButtonNoteComboBoxes();
                        
                        // Update channel and device selectors
                        UpdateChannelAndDeviceSelectors();
                        
                        LogMidiEvent($"Chord mappings loaded from {dialog.FileName}");
                        MessageBox.Show("Chord mappings loaded successfully!", "Success", 
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        LogMidiEvent("No chord mappings found in the selected file.");
                        MessageBox.Show("No chord mappings found in the selected file.", 
                                      "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading chord mappings: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MappingManager_ModeChanged(object? sender, ControllerMode mode)
        {
            // Update both visualizers
            DebugVisualizer?.UpdateModeLEDs(mode);
            TestVisualizer?.UpdateModeLEDs(mode);
            
            // Update window title or other UI elements as needed
            UpdateModeDisplay(mode);
        }

        private void UpdateModeDisplay(ControllerMode mode)
        {
            Dispatcher.Invoke(() => {
                // Update window title
                this.Title = $"XB2MIDI - {mode} Mode";
                
                // Update test mode display (in Visualizer tab)
                var testModeDisplay = this.FindName("TestModeDisplay") as TextBlock;
                if (testModeDisplay != null)
                    testModeDisplay.Text = $"Mode: {mode}";
                
                // Update visualizers - checking for null first
                controllerVisualizer?.UpdateModeLEDs(mode);
                
                if (this.FindName("DebugVisualizer") is BaseControllerVisualizer debugVisualizer)
                {
                    debugVisualizer.UpdateModeLEDs(mode);
                }
                
                if (this.FindName("TestVisualizer") is BaseControllerVisualizer testVisualizer)
                {
                    testVisualizer.UpdateModeLEDs(mode);
                }
                
                // Find tab items for all modes
                var basicMappingTab = this.FindName("BasicMappingTab") as TabItem;
                var chordMappingTab = this.FindName("ChordMappingTab") as TabItem;
                var arpeggioMappingTab = this.FindName("ArpeggioMappingTab") as TabItem;
                var directMappingTab = this.FindName("DirectMappingTab") as TabItem;
                
                // Reset all tab indicators first (now they'll keep their structure but with transparent indicator)
                ClearModeIndicator(basicMappingTab);
                ClearModeIndicator(chordMappingTab);
                ClearModeIndicator(arpeggioMappingTab);
                ClearModeIndicator(directMappingTab);
                
                // Set an indicator for the active mode tab
                switch (mode)
                {
                    case ControllerMode.Basic:
                        SetModeIndicator(basicMappingTab, Colors.DodgerBlue);
                        break;
                    case ControllerMode.Chord:
                        SetModeIndicator(chordMappingTab, Colors.LimeGreen);
                        break;
                    case ControllerMode.Arpeggio:
                        SetModeIndicator(arpeggioMappingTab, Colors.Purple);
                        break;
                    case ControllerMode.Direct:
                        SetModeIndicator(directMappingTab, Colors.Orange);
                        break;
                }
                
                // Log the mode change
                LogMidiEvent($"Mode changed to: {mode}");
            });
            
            Debug.WriteLine($"Updating mode display to: {mode}");
        }

        // Helper methods to set and clear mode indicators on tab headers
        private void SetModeIndicator(TabItem? tab, Color color)
        {
            if (tab == null) return;
            
            // Get the existing header content
            if (tab.Header is string headerText)
            {
                // Create a new header with a consistent layout
                var stackPanel = new StackPanel { 
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

        private void ClearModeIndicator(TabItem? tab)
        {
            if (tab == null) return;
            
            if (tab.Header is string headerText)
            {
                // Create a new header with placeholder for the indicator to maintain consistent width
                var stackPanel = new StackPanel { 
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

        // For initialization, make sure all tabs have the same structure initially
        private void InitializeTabHeaders()
        {
            // Find tab items for all modes
            var basicMappingTab = this.FindName("BasicMappingTab") as TabItem;
            var chordMappingTab = this.FindName("ChordMappingTab") as TabItem;
            var arpeggioMappingTab = this.FindName("ArpeggioMappingTab") as TabItem;
            var directMappingTab = this.FindName("DirectMappingTab") as TabItem;
            
            // Set the shorter tab labels first
            if (basicMappingTab != null) basicMappingTab.Header = "Basic";
            if (chordMappingTab != null) chordMappingTab.Header = "Chord";
            if (arpeggioMappingTab != null) arpeggioMappingTab.Header = "Arp";
            if (directMappingTab != null) directMappingTab.Header = "Direct";
            
            // Initialize all tab headers with placeholders and the new shorter labels
            ClearModeIndicator(basicMappingTab);
            ClearModeIndicator(chordMappingTab);
            ClearModeIndicator(arpeggioMappingTab);
            ClearModeIndicator(directMappingTab);
        }

        private void PopulateMappingDevices()
        {
            var deviceList = new List<string>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                deviceList.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
            }
            
            // Update renamed combo box
            BasicMappingDeviceComboBox.ItemsSource = deviceList;
            if (BasicMappingDeviceComboBox.Items.Count > 0)
            {
                BasicMappingDeviceComboBox.SelectedIndex = 0;
            }
        }

        // New methods for Chord Mode functionality
        private void InitializeChordModeUI()
        {
            // Populate note selection combos
            PopulateNoteComboBoxes();
            
            // Update button note mapping combos
            UpdateButtonNoteComboBoxes();
            
            // Subscribe to ModeState chord events
            modeState.ChordRequested += ModeState_ChordRequested;

            // Also populate channel and device options for each button
            PopulateChannelAndDeviceSelectors();
        }

        private void PopulateNoteComboBoxes()
        {
            // Create list of note names for selection
            var noteNames = new List<string> { 
                "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" 
            };
            
            // Set up test chord root note selection
            if (TestChordRootCombo != null)
            {
                for (int octave = 2; octave <= 6; octave++)
                {
                    foreach (var note in noteNames)
                    {
                        TestChordRootCombo.Items.Add($"{note}{octave}");
                    }
                }
                TestChordRootCombo.SelectedIndex = 24; // Default to C4
            }
            
            // Populate all note selection comboboxes for button mapping
            PopulateButtonNoteCombo(AButtonNoteCombo);
            PopulateButtonNoteCombo(BButtonNoteCombo);
            PopulateButtonNoteCombo(XButtonNoteCombo);
            PopulateButtonNoteCombo(YButtonNoteCombo);
            PopulateButtonNoteCombo(DPadUpNoteCombo);
            PopulateButtonNoteCombo(DPadDownNoteCombo);
            PopulateButtonNoteCombo(DPadLeftNoteCombo);
            PopulateButtonNoteCombo(DPadRightNoteCombo);
        }

        private void PopulateButtonNoteCombo(ComboBox? combo)
        {
            if (combo == null) return;
            
            combo.Items.Clear();
            var noteNames = new List<string> { 
                "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" 
            };
            
            for (int octave = 2; octave <= 6; octave++)
            {
                foreach (var note in noteNames)
                {
                    int noteIndex = noteNames.IndexOf(note);
                    int midiNote = (octave * 12) + noteIndex + 12; // MIDI note calculation
                    combo.Items.Add(new ComboBoxItem {
                        Content = $"{note}{octave} ({midiNote})",
                        Tag = midiNote
                    });
                }
            }
        }

        private void UpdateButtonNoteComboBoxes()
        {
            // Set comboboxes according to current mapping
            UpdateButtonNoteCombo(AButtonNoteCombo, "A");
            UpdateButtonNoteCombo(BButtonNoteCombo, "B");
            UpdateButtonNoteCombo(XButtonNoteCombo, "X");
            UpdateButtonNoteCombo(YButtonNoteCombo, "Y");
            UpdateButtonNoteCombo(DPadUpNoteCombo, "DPadUp");
            UpdateButtonNoteCombo(DPadDownNoteCombo, "DPadDown");
            UpdateButtonNoteCombo(DPadLeftNoteCombo, "DPadLeft");
            UpdateButtonNoteCombo(DPadRightNoteCombo, "DPadRight");
            
            // Add change handlers
            AddNoteComboChangeHandler(AButtonNoteCombo, "A");
            AddNoteComboChangeHandler(BButtonNoteCombo, "B");
            AddNoteComboChangeHandler(XButtonNoteCombo, "X");
            AddNoteComboChangeHandler(YButtonNoteCombo, "Y");
            AddNoteComboChangeHandler(DPadUpNoteCombo, "DPadUp");
            AddNoteComboChangeHandler(DPadDownNoteCombo, "DPadDown");
            AddNoteComboChangeHandler(DPadLeftNoteCombo, "DPadLeft");
            AddNoteComboChangeHandler(DPadRightNoteCombo, "DPadRight");
        }

        private void UpdateButtonNoteCombo(ComboBox? combo, string buttonName)
        {
            if (combo == null || modeState?.ButtonNoteMap == null) return;
            
            if (modeState.ButtonNoteMap.TryGetValue(buttonName, out byte noteValue))
            {
                // Find the matching item in the combo box
                foreach (ComboBoxItem item in combo.Items)
                {
                    if (item.Tag is int midiNote && midiNote == noteValue)
                    {
                        combo.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void AddNoteComboChangeHandler(ComboBox? combo, string buttonName)
        {
            if (combo == null) return;
            
            combo.SelectionChanged += (s, e) => {
                if (combo.SelectedItem is ComboBoxItem selected && selected.Tag is int midiNote)
                {
                    // Update the mapping
                    modeState.ButtonNoteMap[buttonName] = (byte)midiNote;
                    LogMidiEvent($"Updated {buttonName} button note mapping to {selected.Content}");
                }
            };
        }

        private void PopulateChannelAndDeviceSelectors()
        {
            // Get references to all channel and device combo boxes
            var buttonNames = new[] { "A", "B", "X", "Y", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" };
            
            foreach (var buttonName in buttonNames)
            {
                var channelCombo = this.FindName($"{buttonName}ChannelCombo") as ComboBox;
                var deviceCombo = this.FindName($"{buttonName}DeviceCombo") as ComboBox;
                
                if (channelCombo != null)
                {
                    // Populate MIDI channels (1-16)
                    for (int i = 1; i <= 16; i++)
                    {
                        channelCombo.Items.Add(i);
                    }
                    
                    // Set initial selection based on ModeState
                    byte channel = 0;
                    if (modeState.ButtonChannelMap.TryGetValue(buttonName, out channel))
                    {
                        channelCombo.SelectedIndex = channel; // Select the appropriate channel (0-based)
                    }
                    else
                    {
                        channelCombo.SelectedIndex = 0; // Default to channel 1
                    }
                    
                    // Add change handler
                    channelCombo.SelectionChanged += (s, e) => {
                        if (channelCombo.SelectedIndex >= 0)
                        {
                            byte selectedChannel = (byte)channelCombo.SelectedIndex;
                            modeState.ButtonChannelMap[buttonName] = selectedChannel;
                            LogMidiEvent($"Updated {buttonName} button MIDI channel to {selectedChannel + 1}");
                        }
                    };
                }
                
                if (deviceCombo != null)
                {
                    // Populate with available MIDI devices
                    for (int i = 0; i < MidiOut.NumberOfDevices; i++)
                    {
                        deviceCombo.Items.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
                    }
                    
                    // Set initial selection based on ModeState
                    int deviceIndex = 0;
                    if (modeState.ButtonDeviceMap.TryGetValue(buttonName, out deviceIndex))
                    {
                        if (deviceIndex < deviceCombo.Items.Count)
                            deviceCombo.SelectedIndex = deviceIndex;
                        else
                            deviceCombo.SelectedIndex = 0;
                    }
                    else
                    {
                        deviceCombo.SelectedIndex = 0;
                    }
                    
                    // Add change handler
                    deviceCombo.SelectionChanged += (s, e) => {
                        if (deviceCombo.SelectedIndex >= 0)
                        {
                            modeState.ButtonDeviceMap[buttonName] = deviceCombo.SelectedIndex;
                            string deviceName = deviceCombo.SelectedItem.ToString() ?? "";
                            LogMidiEvent($"Updated {buttonName} button MIDI device to {deviceName}");
                        }
                    };
                }
            }
        }

        private void UpdateChannelAndDeviceSelectors()
        {
            // Update channel and device selectors based on current modeState
            var buttonNames = new[] { "A", "B", "X", "Y", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" };
            
            foreach (var buttonName in buttonNames)
            {
                var channelCombo = this.FindName($"{buttonName}ChannelCombo") as ComboBox;
                var deviceCombo = this.FindName($"{buttonName}DeviceCombo") as ComboBox;
                
                if (channelCombo != null && modeState?.ButtonChannelMap != null && 
                    modeState.ButtonChannelMap.TryGetValue(buttonName, out byte channel))
                {
                    channelCombo.SelectedIndex = channel;
                }
                
                if (deviceCombo != null && modeState?.ButtonDeviceMap != null && 
                    modeState.ButtonDeviceMap.TryGetValue(buttonName, out int deviceIndex))
                {
                    if (deviceIndex < deviceCombo.Items.Count)
                        deviceCombo.SelectedIndex = deviceIndex;
                }
            }
        }

        private void ResetChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (modeState != null)
            {
                // Reset to defaults
                modeState.ResetButtonMappings();
                
                // Update UI
                UpdateButtonNoteComboBoxes();
                UpdateChannelAndDeviceSelectors();
                
                LogMidiEvent("Chord mappings reset to defaults");
            }
        }

        private void SaveChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (mappingManager == null) return;
            
            try
            {
                // Save current chord settings to mapping manager
                mappingManager.SaveChordMapping(modeState);
                
                // Ask user where to save the file
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    Title = "Save Chord Mappings"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // Save to file
                    mappingManager.SaveMappings(dialog.FileName);
                    LogMidiEvent($"Chord mappings saved to {dialog.FileName}");
                    MessageBox.Show("Chord mappings saved successfully!", "Success", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving chord mappings: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ModeState_ChordRequested(object? sender, ChordEventArgs e)
        {
            if (midiOutput == null) return;

            // Calculate note names for logging
            string rootNoteName = GetNoteName(e.RootNote);
            
            // Get per-button device and channel settings
            byte channel = e.Channel;
            int deviceIndex = e.DeviceIndex;
            
            // Use a fixed velocity value of 100 instead of reading from the slider
            byte velocity = 100;
            
            if (e.IsOn)
            {
                // Always play the root note
                midiOutput.SendNoteOn(deviceIndex, channel, e.RootNote, velocity);
                
                // Only play additional notes if this is a chord (not root only)
                if (!e.PlayRootOnly)
                {
                    midiOutput.SendNoteOn(deviceIndex, channel, e.ThirdNote, velocity);
                    midiOutput.SendNoteOn(deviceIndex, channel, e.FifthNote, velocity);
                    
                    // Play seventh note if this is a seventh chord
                    if (e.HasSeventh)
                    {
                        midiOutput.SendNoteOn(deviceIndex, channel, e.SeventhNote, velocity);
                    }
                    
                    // Play ninth note if this is a ninth chord
                    if (e.HasNinth)
                    {
                        midiOutput.SendNoteOn(deviceIndex, channel, e.NinthNote, velocity);
                    }
                    
                    LogChordActivity($"Chord played: {rootNoteName} ({GetChordType(e)}) on device {deviceIndex}, channel {channel + 1}", true);
                }
                else
                {
                    LogChordActivity($"Note played: {rootNoteName} on device {deviceIndex}, channel {channel + 1}", true);
                }
            }
            else
            {
                // Always send note-off for root note
                midiOutput.SendNoteOff(deviceIndex, channel, e.RootNote);
                
                // Send note-offs for other notes if this was a chord
                if (!e.PlayRootOnly)
                {
                    midiOutput.SendNoteOff(deviceIndex, channel, e.ThirdNote);
                    midiOutput.SendNoteOff(deviceIndex, channel, e.FifthNote);
                    
                    // Turn off seventh note if this was a seventh chord
                    if (e.HasSeventh)
                    {
                        midiOutput.SendNoteOff(deviceIndex, channel, e.SeventhNote);
                    }
                    
                    // Turn off ninth note if this was a ninth chord
                    if (e.HasNinth)
                    {
                        midiOutput.SendNoteOff(deviceIndex, channel, e.NinthNote);
                    }
                    
                    LogChordActivity($"Chord released: {rootNoteName}", false);
                }
                else
                {
                    LogChordActivity($"Note released: {rootNoteName}", false);
                }
            }
        }

        private string GetChordType(ChordEventArgs e)
        {
            int third = e.ThirdNote - e.RootNote;
            int fifth = e.FifthNote - e.RootNote;
            
            if (e.HasNinth)
            {
                int seventh = e.SeventhNote - e.RootNote;
                if (third == 4 && seventh == 11) return "major 9th";
                if (third == 3 && seventh == 10) return "minor 9th";
            }
            else if (e.HasSeventh)
            {
                int seventh = e.SeventhNote - e.RootNote;
                if (third == 4 && seventh == 11) return "major 7th";
                if (third == 3 && seventh == 10) return "minor 7th";
                if (third == 4 && seventh == 10) return "dominant 7th";
            }
            
            if (third == 4 && fifth == 7) return "major";
            if (third == 3 && fifth == 7) return "minor";
            if (third == 3 && fifth == 6) return "diminished";
            
            return "custom";
        }

        private string GetNoteName(byte noteNumber)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (noteNumber / 12) - 1;
            int noteIndex = noteNumber % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        private int GetSelectedMidiDeviceIndex()
        {
            // Use the same device as basic mapping for consistency
            if (BasicMappingDeviceComboBox?.SelectedItem != null)
            {
                string deviceString = BasicMappingDeviceComboBox.SelectedItem.ToString() ?? "";
                if (int.TryParse(deviceString.Split(':')[0], out int deviceIndex))
                {
                    return deviceIndex;
                }
            }
            return 0; // Default to first device
        }

        private void LogChordActivity(string message, bool isPlayed)
        {
            Dispatcher.Invoke(() => {
                if (ChordActivityLog != null)
                {
                    ChordActivityLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");
                    if (ChordActivityLog.Items.Count > 100)
                        ChordActivityLog.Items.RemoveAt(ChordActivityLog.Items.Count - 1);
                }
            });
            
            // Also log to main MIDI event log
            LogMidiEvent(message);
        }

        // Event handlers for UI elements in Chord Mode tab
        private void TestMajorChord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void TestMinorChord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void Test7thChord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void TestDimChord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void TestMajor7Chord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void TestMinor7Chord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void TestMajor9Chord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void TestMinor9Chord_Click(object sender, RoutedEventArgs e)
        {
            ChordPreset_Click((Button)sender, e);
        }

        private void PlayCustomChord_Click(object sender, RoutedEventArgs e)
        {
            if (midiOutput == null || TestChordRootCombo?.SelectedItem == null) return;
            
            // Get the root note
            string noteText = TestChordRootCombo.SelectedItem.ToString() ?? "C4";
            byte rootNote = GetMidiNoteFromName(noteText);
            
            // Create a list to hold all the notes in our chord
            List<byte> chordNotes = new List<byte>();
            
            // Add root note only if toggled on
            if (RootToggle.IsChecked == true)
                chordNotes.Add(rootNote);
            
            // Add other notes based on toggles
            if (MajThirdToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 4)); // Major 3rd
                
            if (MinThirdToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 3)); // Minor 3rd
                
            // Special case for Sus4
            if (!MajThirdToggle.IsChecked == true && !MinThirdToggle.IsChecked == true)
                if (FifthToggle.IsChecked == true || FlatFifthToggle.IsChecked == true)
                    chordNotes.Add((byte)(rootNote + 5)); // Perfect 4th (for sus4 chord)
                
            if (FifthToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 7)); // Perfect 5th
                
            if (FlatFifthToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 6)); // Diminished 5th
                
            if (SixthToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 9)); // Major 6th
                
            if (DomSeventhToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 10)); // Dominant 7th (minor 7th)
                
            if (MajSeventhToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 11)); // Major 7th
                
            if (NinthToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 14)); // Major 9th
                
            if (FlatNinthToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 13)); // Flat 9th
            
            // Skip if no notes are selected
            if (chordNotes.Count == 0)
                return;
                
            // Play the chord
            int deviceIndex = GetSelectedMidiDeviceIndex();
            byte velocity = 100;
            
            // Send note-on for all notes in the chord
            foreach (byte note in chordNotes)
            {
                midiOutput.SendNoteOn(deviceIndex, 0, note, velocity);
            }
            
            // Generate chord name for logging
            string chordName = DetermineChordName(chordNotes, rootNote);
            LogChordActivity($"Custom chord played: {GetNoteName(rootNote)} {chordName}", true);
            
            // Schedule note-off after 500ms
            Task.Delay(500).ContinueWith(_ => {
                foreach (byte note in chordNotes)
                {
                    midiOutput.SendNoteOff(deviceIndex, 0, note);
                }
            });
        }

        private void ChordPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Reset all note toggles first
                ClearChordToggles();
                
                // Always set root for presets
                RootToggle.IsChecked = true;
                
                // Configure the chord based on preset
                switch (button.Content.ToString())
                {
                    case "Major":
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        break;
                        
                    case "Minor":
                        MinThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        break;
                        
                    case "Maj7":
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        MajSeventhToggle.IsChecked = true;
                        break;
                        
                    case "Min7":
                        MinThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        DomSeventhToggle.IsChecked = true;
                        break;
                        
                    case "Dom7":
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        DomSeventhToggle.IsChecked = true;
                        break;
                        
                    case "Dim":
                        MinThirdToggle.IsChecked = true;
                        FlatFifthToggle.IsChecked = true;
                        break;
                        
                    case "Sus4":
                        // In Sus4, we omit the third and add a fourth
                        MajThirdToggle.IsChecked = false;
                        MinThirdToggle.IsChecked = false;
                        FifthToggle.IsChecked = true;
                        break;
                        
                    case "Add9":
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        NinthToggle.IsChecked = true;
                        break;
                        
                    case "6":
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        SixthToggle.IsChecked = true;
                        break;
                        
                    case "m6":
                        MinThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        SixthToggle.IsChecked = true;
                        break;
                }
                
                // Play the chord immediately
                PlayCustomChord_Click(sender, e);
            }
        }

        private void ClearChordToggles()
        {
            // Make root optional but leave it on by default
            RootToggle.IsChecked = true;
            MajThirdToggle.IsChecked = false;
            MinThirdToggle.IsChecked = false;
            FifthToggle.IsChecked = false;
            FlatFifthToggle.IsChecked = false;
            SixthToggle.IsChecked = false;
            DomSeventhToggle.IsChecked = false;
            MajSeventhToggle.IsChecked = false;
            NinthToggle.IsChecked = false;
            FlatNinthToggle.IsChecked = false;
        }

        private string DetermineChordName(List<byte> chordNotes, byte rootNote)
        {
            if (chordNotes.Count == 0)
                return "(no notes)";
                
            // Check if the chord contains the root note
            bool hasRoot = chordNotes.Contains(rootNote);
            
            // If only playing a single note other than the root, return its interval name
            if (chordNotes.Count == 1 && !hasRoot)
            {
                int interval = chordNotes[0] - rootNote;
                return $"({GetIntervalName(interval)})";
            }
                
            // Check for all possible chord components
            bool hasMinorThird = chordNotes.Contains((byte)(rootNote + 3));
            bool hasMajorThird = chordNotes.Contains((byte)(rootNote + 4));
            bool hasPerfectFourth = chordNotes.Contains((byte)(rootNote + 5));
            bool hasDiminishedFifth = chordNotes.Contains((byte)(rootNote + 6));
            bool hasPerfectFifth = chordNotes.Contains((byte)(rootNote + 7));
            bool hasSixth = chordNotes.Contains((byte)(rootNote + 9));
            bool hasDominantSeventh = chordNotes.Contains((byte)(rootNote + 10));
            bool hasMajorSeventh = chordNotes.Contains((byte)(rootNote + 11));
            bool hasFlatNinth = chordNotes.Contains((byte)(rootNote + 13));
            bool hasNinth = chordNotes.Contains((byte)(rootNote + 14));
            
            // Determine basic chord quality
            string quality = "";
            
            // Custom handling for chords without root
            if (!hasRoot)
            {
                return "(rootless voicing)";
            }
            
            if (!hasMajorThird && !hasMinorThird && hasPerfectFourth)
            {
                quality = "sus4";
            }
            else if (hasMinorThird && hasDiminishedFifth)
            {
                quality = "dim";
            }
            else if (hasMinorThird)
            {
                quality = "m";
            }
            else if (hasMajorThird)
            {
                quality = ""; // Major is the default with no prefix
            }
            else if (!hasMajorThird && !hasMinorThird && !hasPerfectFourth && hasPerfectFifth)
            {
                quality = "5"; // Power chord (just root and fifth)
            }
            else if (chordNotes.Count == 1) // Only the root note
            {
                return "(root only)";
            }
            
            // Add extensions
            if (hasMajorSeventh)
            {
                quality += "maj7";
            }
            else if (hasDominantSeventh)
            {
                quality += "7";
            }
            
            if (hasSixth && !hasMajorSeventh && !hasDominantSeventh)
            {
                quality += "6";
            }
            
            // Add 9th if present
            if (hasNinth)
            {
                // If there's no 7th, it's an add9
                if (!hasMajorSeventh && !hasDominantSeventh)
                {
                    quality += "add9";
                }
                else
                {
                    quality += "9";
                }
            }
            else if (hasFlatNinth)
            {
                quality += "♭9";
            }
            
            return quality;
        }

        private string GetIntervalName(int semitones)
        {
            return semitones switch
            {
                0 => "root",
                1 => "minor 2nd",
                2 => "major 2nd",
                3 => "minor 3rd",
                4 => "major 3rd",
                5 => "perfect 4th",
                6 => "diminished 5th",
                7 => "perfect 5th",
                8 => "augmented 5th",
                9 => "major 6th",
                10 => "minor 7th",
                11 => "major 7th",
                12 => "octave",
                13 => "flat 9th",
                14 => "9th",
                _ => $"{semitones} semitones"
            };
        }

        private byte GetMidiNoteFromName(string noteText)
        {
            char noteLetter = noteText[0];
            bool isSharp = noteText.Length > 2 && noteText[1] == '#';
            int octave = int.Parse(noteText[noteText.Length - 1].ToString());
            
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int noteIndex = Array.FindIndex(noteNames, n => n.StartsWith(noteLetter.ToString()));
            if (isSharp) noteIndex++;
            
            return (byte)((octave + 1) * 12 + noteIndex);
        }

        private void NoteToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton clickedButton)
            {
                // Handle exclusive toggling between major and minor third
                if (clickedButton == MajThirdToggle && clickedButton.IsChecked == true)
                {
                    MinThirdToggle.IsChecked = false;
                }
                else if (clickedButton == MinThirdToggle && clickedButton.IsChecked == true)
                {
                    MajThirdToggle.IsChecked = false;
                }
                
                // Handle exclusive toggling between fifth and flat fifth
                if (clickedButton == FifthToggle && clickedButton.IsChecked == true)
                {
                    FlatFifthToggle.IsChecked = false;
                }
                else if (clickedButton == FlatFifthToggle && clickedButton.IsChecked == true)
                {
                    FifthToggle.IsChecked = false;
                }
                
                // Handle exclusive toggling between dominant and major seventh
                if (clickedButton == DomSeventhToggle && clickedButton.IsChecked == true)
                {
                    MajSeventhToggle.IsChecked = false;
                }
                else if (clickedButton == MajSeventhToggle && clickedButton.IsChecked == true)
                {
                    DomSeventhToggle.IsChecked = false;
                }
                
                // Handle exclusive toggling between ninth and flat ninth
                if (clickedButton == NinthToggle && clickedButton.IsChecked == true)
                {
                    FlatNinthToggle.IsChecked = false;
                }
                else if (clickedButton == FlatNinthToggle && clickedButton.IsChecked == true)
                {
                    NinthToggle.IsChecked = false;
                }
            }
        }

        private void ClearChord_Click(object sender, RoutedEventArgs e)
        {
            ClearChordToggles();
        }
    }
}
