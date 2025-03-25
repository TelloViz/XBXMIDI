using System;
using System.Windows;
using System.Windows.Media; // Add this for CompositionTarget
using System.IO; // Add this for File
using System.Windows.Controls; // Add this for SelectionChangedEventArgs

using NAudio.Midi;
using System.Collections.ObjectModel;

using XB2Midi.Models;

namespace XB2Midi.Views
{
    public partial class MainWindow : Window
    {
        private XboxController? controller;
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog;
        private Dictionary<string, byte> _lastTriggerValues = new Dictionary<string, byte>();

        public MainWindow()
        {
            InitializeComponent();
            midiLog = new ObservableCollection<string>();

            try
            {
                controller = new XboxController();
                midiOutput = new MidiOutput();
                mappingManager = new MappingManager(midiOutput);

                controller.InputChanged += Controller_InputChanged;
                CompositionTarget.Rendering += (s, e) => controller?.Update();

                // Load default mappings if exist
                if (File.Exists("default_mappings.json"))
                    mappingManager?.LoadMappings("default_mappings.json");

                MidiActivityLog.ItemsSource = midiLog;

                RefreshMidiDevices();

                // Fix null reference warning
                if (mappingManager != null)
                {
                    mappingManager.MappingsChanged += (s, e) => 
                    {
                        MappingsView?.UpdateMappings(mappingManager.GetCurrentMappings());
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}");
                Close();
            }
        }

        private void Controller_InputChanged(object? sender, ControllerInputEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Only log button events and significant thumbstick movements
                    bool shouldLog = e.InputType == ControllerInputType.Button ||
                                   e.InputType == ControllerInputType.Trigger ||
                                   (e.InputType == ControllerInputType.Thumbstick && IsSignificantThumbstickMovement(e.Value));

                    if (shouldLog)
                    {
                        InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                        if (InputLog.Items.Count > 100) 
                            InputLog.Items.RemoveAt(InputLog.Items.Count - 1);
                    }

                    // Update visual display
                    if (Visualizer.Content is ControllerVisualizer visualizer)
                    {
                        visualizer.UpdateControl(e);
                    }

                    try
                    {
                        mappingManager?.HandleControllerInput(e);
                    }
                    catch (Exception ex)
                    {
                        LogMidiEvent($"MIDI Error: {ex.Message}");
                        MessageBox.Show($"MIDI Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical Error: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private bool IsSignificantThumbstickMovement(object value)
        {
            // Check if value can be used as a dynamic object with X and Y properties
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
            
            // Only log when button is pressed
            if (isPressed)
            {
                LogMidiEvent($"Button {button} triggered");
            }
        }

        private void HandleTriggerMidi(string trigger, object value)
        {
            if (midiOutput == null) return;
            
            byte rawValue = Convert.ToByte(value);
            byte lastValue = _lastTriggerValues.ContainsKey(trigger) ? _lastTriggerValues[trigger] : (byte)0;
            
            // Store raw value for next comparison
            _lastTriggerValues[trigger] = rawValue;
            
            // Ensure we hit maximum when trigger is pressed hard
            if (rawValue == 255)
            {
                var maxArgs = new ControllerInputEventArgs(
                    ControllerInputType.Trigger,
                    trigger,
                    127  // Maximum MIDI value
                );
                mappingManager?.HandleControllerInput(maxArgs);
                LogMidiEvent($"Trigger {trigger}: MAX");
                return;
            }
            
            // Smoothing factor (0-1), higher = less smoothing
            const float SMOOTH_FACTOR = 0.8f;  // Increased from 0.7 for faster response
            
            // Apply smoothing only for non-maximum values
            float smoothedValue = (lastValue * (1 - SMOOTH_FACTOR)) + (rawValue * SMOOTH_FACTOR);
            byte controlValue = (byte)Math.Round((smoothedValue / 255.0) * 127);
            
            var normalArgs = new ControllerInputEventArgs(
                ControllerInputType.Trigger,
                trigger,
                controlValue
            );
            
            mappingManager?.HandleControllerInput(normalArgs);
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
            MidiDeviceComboBox.Items.Clear();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                MidiDeviceComboBox.Items.Add(MidiOut.DeviceInfo(i).ProductName);
            }
        }

        private void MidiDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MidiDeviceComboBox.SelectedIndex != -1)
            {
                try
                {
                    midiOutput?.SetDevice(MidiDeviceComboBox.SelectedIndex);
                    ConnectionStatus.Text = "Connected";
                    LogMidiEvent("Connected to " + MidiDeviceComboBox.SelectedItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error connecting to MIDI device: " + ex.Message);
                    ConnectionStatus.Text = "Connection Failed";
                }
            }
        }

        private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshMidiDevices();
        }

        private void LogMidiEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Only add to MIDI log, not debug log
                midiLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(0);
            });
        }

        // Example method to send MIDI message
        private void SendMidiMessage(int channel, int noteNumber, int velocity)
        {
            if (midiOutput == null) return;
            
            try
            {
                // Use midiOutput instead of midiOut
                midiOutput.SendNoteOn((byte)channel, (byte)noteNumber, (byte)velocity);
                LogMidiEvent($"Note On - Channel: {channel}, Note: {noteNumber}, Velocity: {velocity}");
            }
            catch (Exception ex)
            {
                LogMidiEvent($"Error sending MIDI: {ex.Message}");
            }
        }

        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            string controllerInput = (ControllerInputComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            
            // Parse channel
            if (!int.TryParse(MidiChannelTextBox.Text, out int channel) || channel < 1 || channel > 16)
            {
                MessageBox.Show("Please enter a valid MIDI channel (1-16)");
                return;
            }

            // Determine message type first
            MidiMessageType messageType = midiType switch
            {
                "Note" => MidiMessageType.Note,
                "Control Change" => MidiMessageType.ControlChange,
                "Pitch Bend" => MidiMessageType.PitchBend,
                _ => MidiMessageType.Note
            };

            // Create mapping with pre-determined values
            var mapping = new MidiMapping
            {
                ControllerInput = controllerInput.Replace(" Button", "").Replace(" ", ""),
                MessageType = messageType,
                Channel = (byte)(channel - 1), // Convert to 0-based channel number
                MinValue = 0,
                MaxValue = messageType == MidiMessageType.PitchBend ? 16383 : 127
            };

            // Only validate and set value for Note and CC messages
            if (mapping.MessageType != MidiMessageType.PitchBend)
            {
                if (!int.TryParse(MidiValueTextBox.Text, out int midiValue) || midiValue < 0 || midiValue > 127)
                {
                    MessageBox.Show("Please enter a valid MIDI value (0-127)");
                    return;
                }

                if (mapping.MessageType == MidiMessageType.Note)
                {
                    mapping.NoteNumber = (byte)midiValue;
                }
                else
                {
                    mapping.ControllerNumber = (byte)midiValue;
                }
            }

            // Add mapping to manager
            mappingManager?.HandleMapping(mapping);

            // Log mapping creation
            LogMidiEvent($"Added mapping: {controllerInput} -> {midiType}" + 
                (mapping.MessageType != MidiMessageType.PitchBend ? $" ({MidiValueTextBox.Text})" : ""));
        }

        private void MidiTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MidiValueTextBox == null) return;
            
            string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            bool isPitchBend = midiType == "Pitch Bend";
            
            MidiValueTextBox.IsEnabled = !isPitchBend;
            MidiValueTextBox.Text = isPitchBend ? "" : MidiValueTextBox.Text;
            MidiValueTextBox.ToolTip = isPitchBend ? "Not needed for Pitch Bend" : "Note/CC number (0-127)";
        }
    }
}
