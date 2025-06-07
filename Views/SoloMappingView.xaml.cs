using System;
using System.Windows.Controls;
using XB2Midi.Utilities;
using XB2Midi.Models;
using NAudio.Midi; // Add this import

namespace XB2Midi.Views
{
    public partial class SoloMappingView : UserControl
    {
        private MidiOutput? midiOutput;
        private SoloMappingManager? soloMappingManager;

        // Current state
        private string currentKey = "C";
        private string currentMode = "Major";
        private int currentOctave = 4;

        public SoloMappingView()
        {
            InitializeComponent();
            
            // Defer the initial population until after initialization
            Loaded += (s, e) => 
            {
                UpdateNoteMappings();
            };
        }

        public void Initialize(MidiOutput midiOutput, SoloMappingManager mappingManager)
        {
            this.midiOutput = midiOutput;
            this.soloMappingManager = mappingManager;

            // Initialize all UI components
            InitializeSoloUI();

            // Enable all dropdowns after initialization
            EnableAllDropdowns(true);
        }

        private void InitializeSoloUI()
        {
            if (midiOutput == null) return;

            // Populate note selection combos
            PopulateNoteDropdowns();

            // Update button note mapping combos
            UpdateButtonNoteComboBoxes();

            // Populate channel and device options for each button
            PopulateChannelAndDeviceSelectors();
        }

        private void UpdateButtonNoteComboBoxes()
        {
            // Get all note dropdowns
            var noteDropdowns = new[] 
            { 
                YButtonNoteCombo, BButtonNoteCombo, AButtonNoteCombo, XButtonNoteCombo,
                DPadUpNoteCombo, DPadRightNoteCombo, DPadDownNoteCombo, DPadLeftNoteCombo 
            };

            // Enable all dropdowns
            foreach (var dropdown in noteDropdowns)
            {
                if (dropdown != null)
                    dropdown.IsEnabled = true;
            }

            // Populate initial note options
            PopulateNoteDropdowns();
        }

        private void PopulateChannelAndDeviceSelectors()
        {
            if (midiOutput == null) return;

            // Get available MIDI devices
            var devices = new Dictionary<int, string>();
            for (int i = 0; i < NAudio.Midi.MidiOut.NumberOfDevices; i++)
            {
                devices[i] = NAudio.Midi.MidiOut.DeviceInfo(i).ProductName;
            }

            // Populate device dropdowns
            PopulateDeviceDropdowns(devices);

            // Populate channel dropdowns
            PopulateChannelDropdowns();

            // Enable all dropdowns
            EnableAllDropdowns(true);
        }

        private void PopulateDeviceDropdowns(Dictionary<int, string> devices)
        {
            var deviceDropdowns = new[] 
            { 
                YDeviceCombo, BDeviceCombo, ADeviceCombo, XDeviceCombo,
                DPadUpDeviceCombo, DPadRightDeviceCombo, DPadDownDeviceCombo, DPadLeftDeviceCombo 
            };

            foreach (var dropdown in deviceDropdowns)
            {
                dropdown.Items.Clear();
                for (int i = 0; i < MidiOut.NumberOfDevices; i++)
                {
                    dropdown.Items.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
                }
                dropdown.SelectedIndex = 0;
            }
        }

        private void PopulateChannelDropdowns()
        {
            var channelDropdowns = new[] 
            { 
                YChannelCombo, BChannelCombo, AChannelCombo, XChannelCombo,
                DPadUpChannelCombo, DPadRightChannelCombo, DPadDownChannelCombo, DPadLeftChannelCombo 
            };

            foreach (var dropdown in channelDropdowns)
            {
                dropdown.Items.Clear();
                for (int i = 1; i <= 16; i++) // Display 1-16 for users, but store 0-15
                {
                    dropdown.Items.Add(new ComboBoxItem 
                    { 
                        Content = i.ToString(), 
                        Tag = i - 1 
                    });
                }
                dropdown.SelectedIndex = 0;
            }
        }

        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            // Input handling will be implemented here
        }

        private void KeySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                currentKey = selectedItem.Content.ToString() ?? "C";
                UpdateNoteMappings();
            }
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                currentMode = selectedItem.Content.ToString() ?? "Major";
                UpdateNoteMappings();
            }
        }

        private void OctaveSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (int.TryParse(selectedItem.Content.ToString(), out int octave))
                {
                    currentOctave = octave;
                    UpdateNoteMappings();
                }
            }
        }

        private void PopulateNoteDropdowns()
        {
            if (KeySelector == null || ModeSelector == null || OctaveSelector == null) return;

            var noteDropdowns = new[] 
            { 
                YButtonNoteCombo, BButtonNoteCombo, AButtonNoteCombo, XButtonNoteCombo,
                DPadUpNoteCombo, DPadRightNoteCombo, DPadDownNoteCombo, DPadLeftNoteCombo 
            };

            // Guard against any null dropdowns
            if (noteDropdowns.Any(d => d == null)) return;

            var scaleNotes = ScaleHelper.GetScaleNotes(currentKey, currentMode, currentOctave);

            // Make sure we have enough notes for all buttons
            if (scaleNotes.Count < 8)
            {
                SoloActivityLog?.Items.Insert(0, "Error: Not enough notes in scale");
                return;
            }

            // Assign each note in the scale to a different button
            for (int i = 0; i < noteDropdowns.Length && i < scaleNotes.Count; i++)
            {
                var dropdown = noteDropdowns[i];
                dropdown.Items.Clear();

                // Add the scale note corresponding to this button's position
                var note = scaleNotes[i];
                dropdown.Items.Add(new ComboBoxItem 
                { 
                    Content = note.noteName,
                    Tag = note.midiNote
                });
                dropdown.SelectedIndex = 0;

                // Enable the dropdown
                dropdown.IsEnabled = true;
            }

            SoloActivityLog?.Items.Insert(0, 
                $"Notes updated - Key: {currentKey}, Mode: {currentMode}, Octave: {currentOctave}");
        }

        private void EnableAllDropdowns(bool enable)
        {
            var allDropdowns = new[] 
            {
                YButtonNoteCombo, BButtonNoteCombo, AButtonNoteCombo, XButtonNoteCombo,
                DPadUpNoteCombo, DPadRightNoteCombo, DPadDownNoteCombo, DPadLeftNoteCombo,
                YChannelCombo, BChannelCombo, AChannelCombo, XChannelCombo,
                DPadUpChannelCombo, DPadRightChannelCombo, DPadDownChannelCombo, DPadLeftChannelCombo,
                YDeviceCombo, BDeviceCombo, ADeviceCombo, XDeviceCombo,
                DPadUpDeviceCombo, DPadRightDeviceCombo, DPadDownDeviceCombo, DPadLeftDeviceCombo
            };

            foreach (var dropdown in allDropdowns)
            {
                if (dropdown != null)
                    dropdown.IsEnabled = enable;
            }
        }

        private void UpdateNoteMappings()
        {
            // Repopulate note dropdowns with new key/mode/octave
            PopulateNoteDropdowns();

            // Log the change
            SoloActivityLog?.Items.Insert(0, 
                $"Settings changed - Key: {currentKey}, Mode: {currentMode}, Octave: {currentOctave}");

            // Ensure we don't keep an unlimited log
            while (SoloActivityLog?.Items.Count > 100)
                SoloActivityLog.Items.RemoveAt(SoloActivityLog.Items.Count - 1);
        }
    }
}
