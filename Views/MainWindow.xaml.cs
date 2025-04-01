using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;  // Add this for KeyEventArgs
using System.Linq;  // Add this for Cast<T>() extension method
using NAudio.Midi;
using System.Collections.ObjectModel;
using System.Threading.Tasks;  // Add this for Task
using XB2Midi.Models;
using System.Diagnostics;
using SharpDX.XInput; // Add this for GamepadButtonFlags

namespace XB2Midi.Views
{
    public partial class MainWindow : Window
    {
        private XboxController? controller;
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();
        private readonly TestControllerSimulator testSimulator;
        private ModeState modeState = new ModeState();
        private ControllerVisualizer controllerVisualizer = new ControllerVisualizer();

        public MainWindow()
        {
            testSimulator = new TestControllerSimulator();
            InitializeTestController();

            InitializeComponent();

            try
            {
                controller = new XboxController();
                midiOutput = new MidiOutput();
                mappingManager = new MappingManager(midiOutput);

                // Initialize the MappingsViewControl after creating mappingManager
                MappingsViewControl.Initialize(mappingManager);

                controller.InputChanged += Controller_InputChanged;
                controller.ConnectionChanged += Controller_ConnectionChanged;
                CompositionTarget.Rendering += (s, e) => controller?.Update();

                if (File.Exists("default_mappings.json"))
                    mappingManager?.LoadMappings("default_mappings.json");

                midiLog = new ObservableCollection<string>();
                MidiActivityLog.ItemsSource = midiLog;

                RefreshMidiDevices();

                if (mappingManager != null)
                {
                    mappingManager.MappingsChanged += (s, e) => 
                    {
                        MappingsViewControl?.UpdateMappings(mappingManager.GetCurrentMappings());
                    };

                    mappingManager.ModeChanged += MappingManager_ModeChanged;
                    
                    // Initialize both the mode display and LEDs with the starting mode
                    var initialMode = mappingManager.CurrentMode;
                    UpdateModeDisplay(initialMode);
                    DebugVisualizer?.UpdateModeLEDs(initialMode);
                    TestVisualizer?.UpdateModeLEDs(initialMode);
                }

                UpdateControllerStatus(controller.IsConnected);

                TestVisualizer.SimulateInput += TestVisualizer_SimulateInput;

                modeState.ChordRequested += (sender, e) =>
                {
                    HandleChordOutput(e.RootNote, e.ThirdNote, e.FifthNote, e.IsOn);
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}");
                Close();
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

            // Update visualizer
            controllerVisualizer?.UpdateControl(e);  // Use ?. operator to safely handle null reference

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

            // For chord mode, check bumper states and handle button inputs
            if (modeState.CurrentMode == ControllerMode.Chord && e.InputType == ControllerInputType.Button)
            {
                var gamepadState = controller?.GetState()?.Gamepad;
                bool leftBumperHeld = gamepadState?.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) ?? false;
                bool rightBumperHeld = gamepadState?.Buttons.HasFlag(GamepadButtonFlags.RightShoulder) ?? false;

                bool handleAsChord = modeState.HandleButtonInput(e.InputName, Convert.ToBoolean(e.Value), leftBumperHeld, rightBumperHeld);
                if (!handleAsChord)
                {
                    return;
                }
            }

            // Handle regular MIDI mapping
            if (mappingManager != null)
            {
                var mapping = mappingManager.GetControllerMapping(e.InputName); // Changed to GetControllerMapping
                if (mapping != null)
                {
                    HandleMidiOutput(mapping, e.Value);
                }
            }
        }

        private void HandleChordOutput(byte rootNote, byte thirdNote, byte fifthNote, bool isOn)
        {
            if (midiOutput == null) return;

            byte velocity = isOn ? (byte)127 : (byte)0;
            byte channel = 0; // You might want to make this configurable

            midiOutput.SendNoteOn(channel, rootNote, velocity, velocity);
            midiOutput.SendNoteOn(channel, thirdNote, velocity, velocity);
            midiOutput.SendNoteOn(channel, fifthNote, velocity, velocity);
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
            var currentDevices = new List<string>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                currentDevices.Add(MidiOut.DeviceInfo(i).ProductName);
            }

            MappingDeviceComboBox.ItemsSource = currentDevices;
        }

        private void MidiDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshMidiDevices();
        }

        private void LogMidiEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                midiLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(0);
            });
        }

        private void SendMidiMessage(int deviceIndex, int channel, int noteNumber, int velocity)
        {
            if (midiOutput == null) return;
            
            try
            {
                midiOutput.SendNoteOn(deviceIndex, (byte)channel, (byte)noteNumber, (byte)velocity);
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
                    MappingDeviceComboBox.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select controller input, MIDI message type, and MIDI device.", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }

                string controllerInput = (ControllerInputComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                
                if (!byte.TryParse(MidiChannelTextBox.Text, out byte channel) || channel < 1 || channel > 16)
                {
                    MessageBox.Show("Please enter a valid MIDI channel (1-16).", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }
                
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
                    MidiDeviceIndex = MappingDeviceComboBox.SelectedIndex,
                    MidiDeviceName = MappingDeviceComboBox.SelectedItem?.ToString() ?? ""
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

                mappingManager?.HandleMapping(mapping);

                MidiChannelTextBox.Text = "";
                MidiValueTextBox.Text = "";

                LogMidiEvent($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType} (Device: {mapping.MidiDeviceName})");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding mapping: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void Controller_ConnectionChanged(object? sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateControllerStatus(isConnected);
            });
        }

        private void UpdateControllerStatus(bool isConnected)
        {
            if (ControllerStatus != null)
            {
                ControllerStatus.Text = isConnected ? "Controller Connected" : "Controller Disconnected";
                ControllerStatus.Foreground = isConnected ? Brushes.Green : Brushes.Red;
            }
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

        private void TestNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MappingDeviceComboBox.SelectedIndex >= 0 && midiOutput != null)
            {
                midiOutput.SendNoteOn(MappingDeviceComboBox.SelectedIndex, 0, 60, 100);
                Task.Delay(100).ContinueWith(_ =>
                {
                    midiOutput.SendNoteOff(MappingDeviceComboBox.SelectedIndex, 0, 60);
                });
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
                
                // Update mode text labels
                if (ModeDisplay != null)
                    ModeDisplay.Text = $"Mode: {mode}";
                    
                if (TestModeDisplay != null)
                    TestModeDisplay.Text = $"Mode: {mode}";
                
                // Update visualizers
                controllerVisualizer?.UpdateModeLEDs(mode);
                DebugVisualizer?.UpdateModeLEDs(mode);
                TestVisualizer?.UpdateModeLEDs(mode);
                
                // Log the mode change
                LogMidiEvent($"Mode changed to: {mode}");
                
                // Force layout update
                InvalidateVisual();
            });
            
            Debug.WriteLine($"Updating mode display to: {mode}");
        }
    }
}
