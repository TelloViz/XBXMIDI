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

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                controller = new XboxController();
                midiOutput = new MidiOutput();
                mappingManager = new MappingManager(midiOutput);

                controller.InputChanged += Controller_InputChanged;
                CompositionTarget.Rendering += (s, e) => controller?.Update();

                // Load default mappings if exist
                if (File.Exists("default_mappings.json"))
                    mappingManager.LoadMappings("default_mappings.json");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}");
                Close();
            }

            midiLog = new ObservableCollection<string>();
            MidiActivityLog.ItemsSource = midiLog;

            RefreshMidiDevices();
        }

        private void Controller_InputChanged(object? sender, ControllerInputEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Update debug log only for significant changes
                    InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                    if (InputLog.Items.Count > 100) InputLog.Items.RemoveAt(InputLog.Items.Count - 1);

                    // Update visual display
                    if (Visualizer.Content is ControllerVisualizer visualizer)
                    {
                        visualizer.UpdateControl(e);
                    }

                    try
                    {
                        // Process MIDI mapping without logging every detail
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
            if (!int.TryParse(MidiValueTextBox.Text, out int midiValue) || midiValue < 0 || midiValue > 127)
            {
                MessageBox.Show("Please enter a valid MIDI value (0-127)");
                return;
            }

            string controllerInput = (ControllerInputComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            
            // Create mapping
            var mapping = new MidiMapping
            {
                ControllerInput = controllerInput.Replace(" Button", "").Replace(" ", ""),
                MessageType = midiType == "Note" ? MidiMessageType.Note : MidiMessageType.ControlChange,
                Channel = 0, // Default to channel 1 (0-based)
                MinValue = 0,
                MaxValue = 127
            };

            // Set appropriate number based on message type
            if (mapping.MessageType == MidiMessageType.Note)
            {
                mapping.NoteNumber = (byte)midiValue;
            }
            else
            {
                mapping.ControllerNumber = (byte)midiValue;
            }

            // Add mapping to manager
            mappingManager?.HandleMapping(mapping);

            // Log only the mapping creation
            LogMidiEvent($"Added mapping: {controllerInput} -> {midiType} ({midiValue})");
        }
    }
}