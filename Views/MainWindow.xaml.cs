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

namespace XB2Midi.Views
{
    public partial class MainWindow : Window
    {
        private XboxController? controller;  // Rename from physicalController
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();
        private TestControllerSimulator testSimulator;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTestController();

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}");
                Close();
            }
        }

        private void InitializeTestController()
        {
            testSimulator = new TestControllerSimulator();
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
            Debug.WriteLine($"Controller Input: {e.InputType} {e.InputName} Value: {e.Value}");
            
            Dispatcher.Invoke(() =>
            {
                // Update appropriate visualizer based on source
                if (sender == controller)  // Updated from physicalController
                {
                    DebugVisualizer?.UpdateControl(e);
                    
                    // Only log physical controller input in the debug tab
                    if (e.InputType != ControllerInputType.Thumbstick || IsSignificantThumbstickMovement(e.Value))
                    {
                        InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputName}: {e.Value}");
                        while (InputLog.Items.Count > 100)
                            InputLog.Items.RemoveAt(InputLog.Items.Count - 1);
                    }
                }

                // Handle MIDI mapping for both controllers
                mappingManager?.HandleControllerInput(e);
            });
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
            ModeDisplay.Text = $"Mode: {mode}";
            TestModeDisplay.Text = $"Mode: {mode}";
        }
    }
}
