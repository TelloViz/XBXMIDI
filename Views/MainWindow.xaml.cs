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
        private MidiOut? midiOut;
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
            Dispatcher.Invoke(() =>
            {
                // Update debug log
                InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                if (InputLog.Items.Count > 100) InputLog.Items.RemoveAt(InputLog.Items.Count - 1);

                // Update visual display
                if (Visualizer.Content is ControllerVisualizer visualizer)
                {
                    visualizer.UpdateControl(e);
                }

                // Process MIDI mapping
                mappingManager?.HandleControllerInput(e);

                // Handle MIDI output based on input type
                switch (e.InputType)
                {
                    case ControllerInputType.Button:
                        HandleButtonMidi(e.InputName, e.Value);
                        break;
                    case ControllerInputType.Trigger:
                        HandleTriggerMidi(e.InputName, e.Value);
                        break;
                    case ControllerInputType.Thumbstick:
                        HandleThumbstickMidi(e.InputName, e.Value);
                        break;
                }
            });
        }

        private void HandleButtonMidi(string button, object value)
        {
            if (midiOutput == null) return;
            bool isPressed = Convert.ToInt32(value) != 0;
            
            // Map buttons to MIDI notes (example mapping)
            byte note = button switch
            {
                "A" => 60, // Middle C
                "B" => 62,
                "X" => 64,
                "Y" => 65,
                _ => 0
            };

            if (note > 0)
            {
                if (isPressed)
                    midiOutput.SendNoteOn(note, 127);
                else
                    midiOutput.SendNoteOff(note);
            }
        }

        private void HandleTriggerMidi(string trigger, object value)
        {
            if (midiOutput == null) return;
            byte controlValue = Convert.ToByte(value);
            
            // Map triggers to CC messages
            byte cc = trigger switch
            {
                "LeftTrigger" => 1,  // Modulation wheel
                "RightTrigger" => 7, // Volume
                _ => 0
            };

            if (cc > 0)
            {
                midiOutput.SendControlChange(cc, controlValue);
            }
        }

        private void HandleThumbstickMidi(string stick, object value)
        {
            if (midiOutput == null) return;
            dynamic pos = value;
            
            if (stick == "LeftThumbstick")
            {
                // X axis to pitch bend
                int bendValue = (int)(pos.X / 32767.0 * 8191);
                midiOutput.SendPitchBend(bendValue);
                
                // Y axis to CC 74 (filter cutoff)
                byte yValue = (byte)((pos.Y + 32768.0) / 65535.0 * 127);
                midiOutput.SendControlChange(74, yValue);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            InputLog.Items.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            controller?.Dispose();
            midiOutput?.Dispose();
            midiOut?.Dispose();
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
                    midiOut?.Dispose();
                    midiOut = new MidiOut(MidiDeviceComboBox.SelectedIndex);
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                midiLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                LastMidiMessage.Text = message;
                
                // Keep only last 100 messages
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(0);
                
                // Auto-scroll to bottom
                MidiActivityLog.ScrollIntoView(midiLog[midiLog.Count - 1]);
            });
        }

        // Example method to send MIDI message
        private void SendMidiMessage(int channel, int noteNumber, int velocity)
        {
            if (midiOut == null) return;
            
            try
            {
                midiOut.Send(MidiMessage.StartNote(noteNumber, velocity, channel).RawData);
                LogMidiEvent($"Note On - Channel: {channel}, Note: {noteNumber}, Velocity: {velocity}");
            }
            catch (Exception ex)
            {
                LogMidiEvent($"Error sending MIDI: {ex.Message}");
            }
        }
    }
}