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
        private string currentKey = "C4";  // Now includes octave
        private string currentMode = "Major";

        private bool isInitializing = true;  // Add this field at the top

        public SoloMappingView()
        {
            InitializeComponent();
            
            // Remove the Loaded event - we'll handle initialization in Initialize method
        }

        public void Initialize(MidiOutput midiOutput, SoloMappingManager mappingManager)
        {
            isInitializing = true;  // Set flag

            this.midiOutput = midiOutput;
            this.soloMappingManager = mappingManager;

            // Remove event handlers during initialization
            KeySelector.SelectionChanged -= KeySelector_SelectionChanged;
            ModeSelector.SelectionChanged -= ModeSelector_SelectionChanged;

            // Initialize selectors
            PopulateKeySelector();
            
            // Initialize other controls
            PopulateChannelAndDeviceSelectors();
            UpdateNoteDropdownsForCurrentKey();
            EnableAllDropdowns(true);

            // Restore event handlers
            KeySelector.SelectionChanged += KeySelector_SelectionChanged;
            ModeSelector.SelectionChanged += ModeSelector_SelectionChanged;

            isInitializing = false;  // Clear flag
        }

        private void PopulateKeySelector()
        {
            if (KeySelector == null) return;

            KeySelector.Items.Clear();
            KeySelector.Items.Add(new ComboBoxItem { Content = "Custom" });

            // Add all notes with octaves
            string[] noteNames = { "C", "C#/Db", "D", "D#/Eb", "E", "F", "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B" };
            for (int octave = 2; octave <= 6; octave++)
            {
                foreach (string note in noteNames)
                {
                    KeySelector.Items.Add(new ComboBoxItem { Content = $"{note}{octave}" });
                }
            }

            // Select C4 by default without triggering the event
            var defaultKey = KeySelector.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == "C4");
            if (defaultKey != null)
            {
                KeySelector.SelectedItem = defaultKey;
                currentKey = "C4"; // Manually set the current key since we're bypassing the event
            }
        }

        private void InitializeSoloUI()
        {
            if (midiOutput == null) return;

            PopulateKeySelector();
            PopulateChannelAndDeviceSelectors();
            
            // Initial note population will be triggered by KeySelector's SelectionChanged event
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
            if (isInitializing) return;

            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string newKey = selectedItem.Content.ToString() ?? "C4";
                if (currentKey != newKey)
                {
                    currentKey = newKey;
                    UpdateNoteDropdownsForCurrentKey();
                }
            }
        }

        private void UpdateNoteDropdownsForCurrentKey()
        {
            if (KeySelector == null || !IsFullyInitialized()) return;

            var noteDropdowns = new[] 
            { 
                YButtonNoteCombo, BButtonNoteCombo, AButtonNoteCombo, XButtonNoteCombo,
                DPadUpNoteCombo, DPadRightNoteCombo, DPadDownNoteCombo, DPadLeftNoteCombo 
            };

            // Guard against null dropdowns
            if (noteDropdowns.Any(d => d == null)) return;

            if (currentKey == "Custom")
            {
                // For Custom, add all possible notes but keep current selections
                foreach (var dropdown in noteDropdowns)
                {
                    var currentSelection = dropdown.SelectedItem as ComboBoxItem;
                    PopulateDropdownWithAllNotes(dropdown);
                    if (currentSelection != null)
                    {
                        // Try to restore previous selection
                        var matchingItem = dropdown.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(x => x.Content.ToString() == currentSelection.Content.ToString());
                        if (matchingItem != null)
                            dropdown.SelectedItem = matchingItem;
                    }
                }
                return;
            }

            // Get key and octave from currentKey (e.g., "C4" -> "C" and 4)
            string keyRoot = new string(currentKey.TakeWhile(c => !char.IsDigit(c)).ToArray());
            int octave = int.Parse(currentKey.Last().ToString());

            // Get scale notes for the current key/mode/octave
            var scaleNotes = ScaleHelper.GetScaleNotesForFullRange(keyRoot, currentMode, octave);

            // Update each dropdown with the appropriate note from the scale
            for (int i = 0; i < noteDropdowns.Length && i < scaleNotes.Count; i++)
            {
                var dropdown = noteDropdowns[i];
                var note = scaleNotes[i];
                
                // Keep current selection to check if it changed
                var currentSelection = dropdown.SelectedItem as ComboBoxItem;
                
                // Populate with all possible notes
                PopulateDropdownWithAllNotes(dropdown);
                
                // Select the scale note
                var matchingItem = dropdown.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(x => x.Tag is int midiNote && midiNote == note.midiNote);
                if (matchingItem != null)
                    dropdown.SelectedItem = matchingItem;

                // Add SelectionChanged handler if not already present
                dropdown.SelectionChanged -= NoteCombo_SelectionChanged;
                dropdown.SelectionChanged += NoteCombo_SelectionChanged;
            }

            LogScaleUpdate();
        }

        private void PopulateDropdownWithAllNotes(ComboBox dropdown)
        {
            var currentSelection = dropdown.SelectedItem as ComboBoxItem;
            dropdown.Items.Clear();

            // Add notes for all octaves
            for (int oct = 2; oct <= 6; oct++)
            {
                var octaveNotes = ScaleHelper.GetAllNotes(oct);
                foreach (var note in octaveNotes)
                {
                    dropdown.Items.Add(new ComboBoxItem 
                    { 
                        Content = note.noteName,
                        Tag = note.midiNote
                    });
                }
            }
        }

        private void NoteCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsFullyInitialized()) return;

            // Get current notes from all dropdowns
            var currentNotes = GetCurrentNoteValues();
            
            // Try to find a matching key/scale
            var matchingKey = ScaleHelper.FindMatchingKey(currentNotes);
            
            if (matchingKey.HasValue)
            {
                // Found a matching key/mode combination
                string newKey = $"{matchingKey.Value.Key}{currentKey.Last()}"; // Keep current octave
                currentKey = newKey;
                currentMode = matchingKey.Value.Mode;

                UpdateKeyAndModeSelectors(newKey, matchingKey.Value.Mode, false);
            }
            else
            {
                // No matching key found, set to Custom
                UpdateKeyAndModeSelectors("Custom", currentMode, false);
            }

            LogNoteChange();
        }

        private bool IsFullyInitialized()
        {
            return !isInitializing && KeySelector != null && ModeSelector != null;
        }

        private List<int> GetCurrentNoteValues()
        {
            var noteValues = new List<int>();
            var noteDropdowns = new[] 
            { 
                YButtonNoteCombo, BButtonNoteCombo, AButtonNoteCombo, XButtonNoteCombo,
                DPadUpNoteCombo, DPadRightNoteCombo, DPadDownNoteCombo, DPadLeftNoteCombo 
            };

            foreach (var dropdown in noteDropdowns)
            {
                if (dropdown.SelectedItem is ComboBoxItem item && item.Tag is int midiNote)
                {
                    noteValues.Add(midiNote);
                }
            }

            return noteValues;
        }

        private void UpdateKeyAndModeSelectors(string key, string mode, bool triggerEvents)
        {
            if (KeySelector == null || ModeSelector == null) return;

            // Temporarily remove event handlers if we don't want to trigger events
            if (!triggerEvents)
            {
                KeySelector.SelectionChanged -= KeySelector_SelectionChanged;
                ModeSelector.SelectionChanged -= ModeSelector_SelectionChanged;
            }

            // Update selections
            foreach (ComboBoxItem item in KeySelector.Items)
            {
                if (item.Content.ToString() == key)
                {
                    KeySelector.SelectedItem = item;
                    break;
                }
            }

            foreach (ComboBoxItem item in ModeSelector.Items)
            {
                if (item.Content.ToString() == mode)
                {
                    ModeSelector.SelectedItem = item;
                    break;
                }
            }

            // Restore event handlers if we removed them
            if (!triggerEvents)
            {
                KeySelector.SelectionChanged += KeySelector_SelectionChanged;
                ModeSelector.SelectionChanged += ModeSelector_SelectionChanged;
            }
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
            if (isInitializing) return;

            // Repopulate note dropdowns with new key/mode/octave
            PopulateNoteDropdowns();

            // Log the change
            SoloActivityLog?.Items.Insert(0, 
                $"Settings changed - Key: {currentKey}, Mode: {currentMode}");

            // Ensure we don't keep an unlimited log
            while (SoloActivityLog?.Items.Count > 100)
                SoloActivityLog.Items.RemoveAt(SoloActivityLog.Items.Count - 1);
        }

        private void PopulateNoteDropdowns()
        {
            var noteDropdowns = new[] 
            { 
                YButtonNoteCombo, BButtonNoteCombo, AButtonNoteCombo, XButtonNoteCombo,
                DPadUpNoteCombo, DPadRightNoteCombo, DPadDownNoteCombo, DPadLeftNoteCombo 
            };

            if (currentKey == "Custom")
            {
                foreach (var dropdown in noteDropdowns)
                {
                    PopulateDropdownWithAllNotes(dropdown);
                }
                return;
            }

            // Get key and octave from currentKey (e.g., "C4" -> "C" and 4)
            string keyRoot = new string(currentKey.TakeWhile(c => !char.IsDigit(c)).ToArray());
            int octave = int.Parse(currentKey.Last().ToString());

            // Get scale notes for the current key/mode/octave
            var scaleNotes = ScaleHelper.GetScaleNotesForFullRange(keyRoot, currentMode, octave);

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
            }
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                currentMode = selectedItem.Content.ToString() ?? "Major";
                UpdateNoteMappings();
            }
        }

        private void LogScaleUpdate()
        {
            SoloActivityLog?.Items.Insert(0, $"Scale updated - Key: {currentKey}, Mode: {currentMode}");
            while (SoloActivityLog?.Items.Count > 100)
                SoloActivityLog.Items.RemoveAt(SoloActivityLog.Items.Count - 1);
        }

        private void LogNoteChange()
        {
            SoloActivityLog?.Items.Insert(0, "Note selection manually changed");
            while (SoloActivityLog?.Items.Count > 100)
                SoloActivityLog.Items.RemoveAt(SoloActivityLog.Items.Count - 1);
        }
    }
}
