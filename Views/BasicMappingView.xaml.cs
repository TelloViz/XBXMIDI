using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Linq;
using NAudio.Midi;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using XB2Midi.Models;
using System.Diagnostics;
using SharpDX.XInput;
using System.Windows.Shapes;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for BasicMappingView.xaml
    /// </summary>
    public partial class BasicMappingView : UserControl
    {
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();


        // In BasicMappingView.xaml.cs
        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            this.midiOutput = output;
            this.mappingManager = mappingManager;
            
            // Add this crucial event subscription
            mappingManager.MappingsChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MappingsListView.ItemsSource = mappingManager.GetCurrentMappings();
                });
            };

            // Register for mapping events
            mappingManager.RegisterMappingEventHandler(LogMidiEvent);

            // Refresh UI with current mappings
            MappingsListView.ItemsSource = mappingManager.GetCurrentMappings();

            // Make sure devices are populated
            PopulateMappingDevices();
        }

        public BasicMappingView()
        {
            InitializeComponent();

            // Initialize the view but don't fully set up yet
            // We'll complete initialization when SetMidiOutput is called

            // Initialize midiLog for activity display
            MidiActivityLog.ItemsSource = midiLog;

            // Initially populate controller inputs - this doesn't need the MidiOutput
            PopulateControllerInputs();

            // Load the device combo box initially - will refresh when MidiOutput is set
            PopulateMappingDevices();

            // Set default MIDI channel
            MidiChannelTextBox.Text = "1";
        }


        // Method to handle incoming controller input from MainWindow
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            if (mappingManager != null)
            {
                mappingManager.HandleControllerInput(e);
            }
        }

        // Add this method to populate controller inputs
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

        // Add a mapping on basic mode
        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ControllerInputComboBox.SelectedItem == null ||
                    MidiTypeComboBox.SelectedItem == null ||
                    BasicMappingDeviceComboBox.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select controller input, MIDI message type, and MIDI device.",
                                  "Validation Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    return;
                }

                string controllerInput = (ControllerInputComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string deviceString = BasicMappingDeviceComboBox.SelectedItem.ToString() ?? "";
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

                LogMidiEvent($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType} on device {mapping.MidiDeviceName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding mapping: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Delete Mapping on Basic Mode Tab
        private void DeleteMapping_Click(object sender, RoutedEventArgs e)
        {
            if (MappingsListView.SelectedItem is MidiMapping selectedMapping && mappingManager != null)
            {
                mappingManager.RemoveMapping(selectedMapping);
                MappingsListView.ItemsSource = mappingManager.GetCurrentMappings();
                LogMidiEvent($"Removed mapping for {selectedMapping.ControllerInput}");
            }
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

            // Update UI
            Dispatcher.Invoke(() =>
            {
                // Ensure we don't keep an unlimited log in memory
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(midiLog.Count - 1);

                if (MidiActivityLog.ItemsSource != midiLog)
                {
                    MidiActivityLog.ItemsSource = null;
                    MidiActivityLog.ItemsSource = midiLog;
                }
            });

            Debug.WriteLine($"MIDI: {message}");
        }

        private void PopulateMappingDevices()
        {
            var deviceList = new List<string>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                deviceList.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
            }

            // Update combo box
            BasicMappingDeviceComboBox.ItemsSource = deviceList;
            if (BasicMappingDeviceComboBox.Items.Count > 0)
            {
                BasicMappingDeviceComboBox.SelectedIndex = 0;
            }
        }
    }
}
