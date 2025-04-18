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
using XB2Midi.ViewModels;

namespace XB2Midi.Views
{

    public partial class ChordMappingView : UserControl
    {
        public ChordMappingViewModel ViewModel { get; private set; }


        private MidiOutput? midiOutput; // MidiOutput instance for sending MIDI messages

        private MappingManager? mappingManager; // MappingManager instance for managing mappings
        private ObservableCollection<string> midiLog = new(); // In-memory log for MIDI events
        private ModeState modeState = new ModeState(); // ModeState instance for managing the current state of the mode

        private MappingTabManager mappingTabManager = new MappingTabManager(); // MappingTabManager instance for managing multiple mappings

        public ChordMappingView()
        {
            InitializeComponent();

            // Create the ViewModel
            ViewModel = new ChordMappingViewModel();

            // Set DataContext
            this.DataContext = ViewModel;

            // Connect the activity log
            ChordActivityLog.ItemsSource = ViewModel.ActivityLog;

            // Pre-initialize ChordInversionComboBox - we can do this without MidiOutput
            PopulateChordInversionComboBox();
        }

        /// <summary>
        /// Sets the MIDI output device for the ChordMappingView.
        /// This method initializes the MappingManager and subscribes to events.
        /// It also calls the method to initialize the UI components.
        /// </summary>
        /// <param name="output">The MIDI output device to be set.</param>
        public void SetMidiOutput(MidiOutput output)
        {
            this.midiOutput = output; // Set the MidiOutput instance

            mappingManager = new MappingManager(output); // Initialize the MappingManager with the MidiOutput

            modeState.ChordRequested += ModeState_ChordRequested; // Subscribe to chord requested event from ModeState

            mappingTabManager.ActiveMappingChanged += MappingTabManager_ActiveMappingChanged; // Subscribe to active mapping changed event from MappingTabManager

            InitializeChordUI(); // Call the method to initialize the UI components
        }

        /// <summary>
        /// Initializes the UI components for the ChordMappingView.
        /// This method populates note selection combos, initializes mapping tabs,
        /// updates button note mapping combos, and populates channel and device options.
        /// It also ensures that the ViewModel is updated with MIDI devices.
        /// </summary>
        private void InitializeChordUI()
        {
            // Initialize all UI components that depend on MidiOutput
            if (midiOutput == null) return;

            // Populate note selection combos
            PopulateNoteComboBoxes();

            // Initialize mapping tabs
            InitializeChordMappingTabs();

            // Update button note mapping combos
            UpdateButtonNoteComboBoxes();

            // Populate channel and device options for each button
            PopulateChannelAndDeviceSelectors();

            // Make sure ViewModel is updated with MIDI devices
            ViewModel.LoadMidiDevices();
        }

        
        /// <summary>
        /// Logs MIDI events to the in-memory log and updates the UI if available.
        /// This method is used to log MIDI events for debugging and monitoring purposes.
        /// </summary>
        /// <param name="message"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            if (mappingManager != null)
            {
                mappingManager.HandleControllerInput(e);
            }
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

        // Toggle Interval in Chord Samlpler on Chord Mode UI
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
        // Clear Chord Sample Toggles on Chord Mode UI
        private void ClearChord_Click(object sender, RoutedEventArgs e)
        {
            ClearChordToggles();
        }

        // Play Sample Chord Button on Chord Mode UI
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

            // Apply inversion if selected
            int inversionLevel = 0;
            if (ChordInversionCombo?.SelectedItem is ComboBoxItem inversionItem && inversionItem.Tag is int level)
            {
                inversionLevel = level;
                chordNotes = ApplyInversion(chordNotes, inversionLevel);
            }

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

            // Add inversion information to the log message
            string inversionText = inversionLevel == 0 ? "" :
                $" ({((ChordInversionCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? $"{inversionLevel} inversion")})";

            LogChordActivity($"Custom chord played: {GetNoteName(rootNote)} {chordName}{inversionText}", true);

            // Schedule note-off after 500ms
            Task.Delay(500).ContinueWith(_ =>
            {
                foreach (byte note in chordNotes)
                {
                    midiOutput.SendNoteOff(deviceIndex, 0, note);
                }
            });
        }

        // New methods for Chord Mode functionality
        private void InitializeChordModeUI()
        {
            // Populate note selection combos
            PopulateNoteComboBoxes();

            // Initialize mapping tabs
            InitializeChordMappingTabs();

            // Update button note mapping combos
            UpdateButtonNoteComboBoxes();

            // Subscribe to ModeState chord events
            modeState.ChordRequested += ModeState_ChordRequested;

            // Also populate channel and device options for each button
            PopulateChannelAndDeviceSelectors();

            // Initialize chord inversion dropdown
            PopulateChordInversionComboBox();
        }

        // I think this is for the Chord Mode UI mapping tabs that allow for multiple chord mode mappings
        private void InitializeChordMappingTabs()
        {
            // Subscribe to mapping changes
            mappingTabManager.ActiveMappingChanged += MappingTabManager_ActiveMappingChanged;

            // Set up initial tab
            RefreshMappingTabs();

            // Apply the initial mapping
            mappingTabManager.ApplyMapping(0, modeState);
        }

        // I think this is for the Chord Mode UI mapping tabs that allow for multiple chord mode mappings
        private void RefreshMappingTabs()
        {
            // Store current selection index to restore it if possible
            int currentIndex = MappingTabsControl.SelectedIndex;

            // Clear existing tabs
            MappingTabsControl.Items.Clear();

            // Add tabs for each mapping
            for (int i = 0; i < mappingTabManager.ChordMappings.Count; i++)
            {
                var mapping = mappingTabManager.ChordMappings[i];

                var tabItem = new TabItem
                {
                    Header = CreateMappingTabHeader(mapping.Name, i),
                    Tag = i
                };

                MappingTabsControl.Items.Add(tabItem);
            }

            // Add the "+" tab if we haven't reached the limit
            if (mappingTabManager.CanAddMapping)
            {
                var addTab = new TabItem
                {
                    Header = "+",
                    Tag = -1
                };

                MappingTabsControl.Items.Add(addTab);
            }

            // Restore selection or set to active mapping
            if (currentIndex >= 0 && currentIndex < MappingTabsControl.Items.Count - 1)
            {
                MappingTabsControl.SelectedIndex = currentIndex;
            }
            else
            {
                MappingTabsControl.SelectedIndex = Math.Min(mappingTabManager.ActiveMappingIndex,
                                                           MappingTabsControl.Items.Count - 2);
            }
        }

        // I think this is for the Chord Mode UI mapping tabs that allow for multiple chord mode mappings
        private object CreateMappingTabHeader(string name, int index)
        {
            var panel = new DockPanel();

            // Add text part (name of the mapping)
            var textBlock = new TextBlock { Text = name, Margin = new Thickness(0, 0, 5, 0) };
            DockPanel.SetDock(textBlock, Dock.Left);
            panel.Children.Add(textBlock);

            // Only add close button if we have more than one mapping and this isn't the "+" tab
            if (mappingTabManager.ChordMappings.Count > 1 && index >= 0)
            {
                var closeButton = new Button
                {
                    Content = "×",
                    Padding = new Thickness(2, 0, 2, 1),
                    Margin = new Thickness(0),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Top,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.Gray,
                    Tag = index
                };

                closeButton.Click += CloseTab_Click;
                DockPanel.SetDock(closeButton, Dock.Right);
                panel.Children.Add(closeButton);
            }

            return panel;
        }

        // I think this is for the Chord Mode UI mapping tabs that allow for multiple chord mode mappings
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int tabIndex)
            {
                // Prevent the event from being handled by the tab selection
                e.Handled = true;

                // Handle tab closing logic
                if (mappingTabManager.RemoveMapping(tabIndex))
                {
                    // Refresh tabs UI
                    RefreshMappingTabs();
                }
            }
        }

        
        /// <summary>
        /// Handles the event when the active mapping changes in the MappingTabManager.
        /// </summary>
        /// <param name="sender"> The sender of the event.</param>
        /// <param name="newIndex"> The index of the new active mapping.</param>
        private void MappingTabManager_ActiveMappingChanged(object sender, int newIndex) {


            mappingTabManager.ApplyMapping(newIndex, modeState); // Apply the new mapping to the mode state

            UpdateButtonNoteComboBoxes();       // Update the button note combo boxes to reflect the new mapping
            UpdateChannelAndDeviceSelectors();  // Update the channel and device selectors to reflect the new mapping

            LogMidiEvent($"Switched to chord mapping: {mappingTabManager.ActiveMapping?.Name ?? "Default"}");
        }

        /// <summary>
        /// Handles the selection change event for the chord mode mapping tabs control.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void MappingTabsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MappingTabsControl.SelectedItem is TabItem selectedTab)                 // Check if the selected item is a TabItem
            {
                if (selectedTab.Tag is int tabIndex)                                    // Check if the Tag is an integer (index)
                {
                    if (tabIndex == -1 && mappingTabManager.CanAddMapping)              // Check if it's the "+" tab
                    {
                        if (mappingTabManager.ActiveMappingIndex >= 0)                  // Save current mapping state before switching
                        {
                            mappingTabManager.UpdateMappingFromState(mappingTabManager.ActiveMappingIndex, modeState); // Update the current mapping with the state
                        }

                        mappingTabManager.AddNewMapping();                              // Add a new mapping

                        RefreshMappingTabs(); // Refresh the tabs to show the new mapping
                    }
                    else if (tabIndex >= 0 && tabIndex < mappingTabManager.ChordMappings.Count) // Check if it's a valid mapping index
                    {
                        if (mappingTabManager.ActiveMappingIndex >= 0) // Save current mapping state before switching
                        {
                            mappingTabManager.UpdateMappingFromState(mappingTabManager.ActiveMappingIndex, modeState); // Update the current mapping with the state
                        }

                        mappingTabManager.ActiveMappingIndex = tabIndex; // Set the new active mapping index
                    }
                }
            }
        }

        /// <summary>
        /// Handles the click event for the "Rename Chord Mappings" button.
        /// </summary>
        /// <param name="sender">The Sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void RenameChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (mappingTabManager.ActiveMapping != null) // Check if there is an active mapping
            {
                string currentName = mappingTabManager.ActiveMapping.Name; // Get the current name of the mapping

                // TODO Check if this is how you actually get the parent window 
                // now that we have migrated this code out of MainWindow.xaml.cs
                Window parentWindow = Window.GetWindow(this);                       // Get the parent window of the current control

                var dialog = new Window                                             // Create a new window for the dialog
                {
                    Title = "Rename Chord Mapping",                                 // Set the title of the dialog
                    SizeToContent = SizeToContent.WidthAndHeight,                   // Set the size to content
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,      // Set the startup location to center the owner
                    Owner = parentWindow,                                           // Set the owner of the dialog to the parent window
                    ResizeMode = ResizeMode.NoResize                                // Disable resizing of the dialog
                };

                var grid = new Grid { Margin = new Thickness(10) };                         // Create a grid for layout
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Add row for label
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Add row for input box

                var label = new TextBlock                                           // Create a label for the input box
                {
                    Text = "Enter a new name for this chord mapping:",              // Set the text of the label
                    Margin = new Thickness(0, 0, 0, 5)                              // Set the margin of the label
                };
                Grid.SetRow(label, 0);                                              // Set the row of the label

                var inputBox = new TextBox                                          // Create a text box for user input
                {
                    Text = currentName,                                             // Set the current name as the text
                    MinWidth = 200,                                                 // Set the minimum width of the text box
                    Margin = new Thickness(0, 0, 0, 10)                             // Set the margin of the text box
                };
                Grid.SetRow(inputBox, 1);                                           // Set the row of the text box

                var buttonPanel = new StackPanel                                    // Create a stack panel for buttons
                {
                    Orientation = Orientation.Horizontal,                           // Set the orientation to horizontal
                    HorizontalAlignment = HorizontalAlignment.Right,                // Align to the right
                    Margin = new Thickness(0, 10, 0, 0)                             // Set the margin of the button panel
                };
                Grid.SetRow(buttonPanel, 2);                                        // Set the row of the button panel

                var okButton = new Button                                           // Create an OK button
                {
                    Content = "OK",                                                 // Set the content of the button
                    IsDefault = true,                                               // Set the button as default
                    MinWidth = 60,                                                  // Set the minimum width of the button
                    Margin = new Thickness(0, 0, 10, 0)                             // Set the margin of the button
                };

                var cancelButton = new Button                                       // Create a Cancel button
                {
                    Content = "Cancel",                                             // Set the content of the button
                    IsCancel = true,                                                // Set the button as cancel
                    MinWidth = 60                                                   // Set the minimum width of the button
                };

                buttonPanel.Children.Add(okButton);                                 // Add the OK button to the button panel
                buttonPanel.Children.Add(cancelButton);                             // Add the Cancel button to the button panel

                grid.Children.Add(label);                                           // Add the label to the grid
                grid.Children.Add(inputBox);                                        // Add the input box to the grid
                grid.Children.Add(buttonPanel);                                     // Add the button panel to the grid

                dialog.Content = grid;                                              // Set the content of the dialog to the grid

                bool dialogResult = false;                                          // Variable to store the dialog result

                okButton.Click += (s, args) =>                                      // Handle OK button click
                {
                    dialogResult = true;                                            // Set the dialog result to true
                    dialog.Close();                                                 // Close the dialog
                };

                dialog.ShowDialog();                                                // Show the dialog and wait for it to close

                if (dialogResult && !string.IsNullOrWhiteSpace(inputBox.Text))      // Check if the dialog was accepted and input is not empty
                {
                    string newName = inputBox.Text.Trim();                          // Get the new name from the input box
                    mappingTabManager.RenameMappingAt(mappingTabManager.ActiveMappingIndex, newName);   // Rename the mapping in the manager

                    RefreshMappingTabs();                                           // Refresh the tabs to show the new name

                    LogMidiEvent($"Renamed chord mapping to: {newName}");           // Log the renaming action
                }
            }
        }

        // Update the save/load methods to work with multiple mappings
        // This says "ChordMappingss" but be careful, i think some of these mapping save/load functions are serving double duty
        private void SaveChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (mappingManager == null) return;

            try
            {
                // First update the active mapping with current state
                mappingTabManager.UpdateMappingFromState(mappingTabManager.ActiveMappingIndex, modeState);

                // Save all mappings to mapping manager
                foreach (var mapping in mappingTabManager.ChordMappings)
                {
                    mappingManager.SaveChordMapping(mapping);
                }

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

        // THis says chord mappings, but its fishy that its nowhere near the rest of the chord mode code
        // Be very careful that this isn't pulling double duty or coupled somewhere in another ifle like MainWindow.xaml
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


        // This seems to be meant for setting Chord Mode mapping to default
        private void ResetChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (modeState != null && mappingTabManager.ActiveMapping != null)
            {
                string currentName = mappingTabManager.ActiveMapping.Name;

                // Reset to defaults
                modeState.ResetButtonMappings();

                // Update the current mapping with the reset state
                mappingTabManager.UpdateMappingFromState(mappingTabManager.ActiveMappingIndex, modeState);

                // Restore the name
                mappingTabManager.RenameMappingAt(mappingTabManager.ActiveMappingIndex, currentName);

                // Update UI
                UpdateButtonNoteComboBoxes();
                UpdateChannelAndDeviceSelectors();

                LogMidiEvent("Chord mapping reset to defaults");
            }
        }

        private void PopulateChordInversionComboBox()
        {
            if (ChordInversionCombo != null)
            {
                ChordInversionCombo.Items.Clear();
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "Root Position", Tag = 0 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "1st Inversion", Tag = 1 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "2nd Inversion", Tag = 2 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "3rd Inversion", Tag = 3 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "4th Inversion", Tag = 4 });
                ChordInversionCombo.SelectedIndex = 0; // Default to Root Position
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

            combo.SelectionChanged += (s, e) =>
            {
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
                    channelCombo.SelectionChanged += (s, e) =>
                    {
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
                    deviceCombo.SelectionChanged += (s, e) =>
                    {
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

        private void ModeState_ChordRequested(object? sender, ChordEventArgs e)
        {
            if (midiOutput == null) return;

            // Calculate note names for logging
            string rootNoteName = GetNoteName(e.RootNote);

            // Get per-button device and channel settings
            byte channel = e.Channel;
            int deviceIndex = e.DeviceIndex;

            // Use the velocity value from ModeState which now gets updated from the left trigger
            byte velocity = e.IsOn ? modeState.GetCurrentVelocity() : (byte)0;

            // IMPORTANT: Get the current inversion directly from ModeState instead of relying on event args
            int inversionLevel = e.InversionLevel;

            List<byte> chordNotes = new List<byte>();

            // Base chord notes
            chordNotes.Add(e.RootNote); // Always include the root note

            if (!e.PlayRootOnly)
            {
                chordNotes.Add(e.ThirdNote);
                chordNotes.Add(e.FifthNote);

                if (e.HasSeventh)
                    chordNotes.Add(e.SeventhNote);

                if (e.HasNinth)
                    chordNotes.Add(e.NinthNote);

                // Apply the inversion if needed
                if (inversionLevel > 0)
                {
                    // Get original notes for debugging
                    var originalNotes = new List<byte>(chordNotes);

                    // Apply inversion
                    chordNotes = ApplyInversion(chordNotes, inversionLevel);

                }
            }

            if (e.IsOn)
            {
                // Play all notes of the chord (already inverted if needed)
                foreach (byte note in chordNotes)
                {
                    midiOutput.SendNoteOn(deviceIndex, channel, note, velocity);
                }

                // Generate chord name with inversion info
                string inversionText = inversionLevel > 0 ? $" ({GetInversionName(inversionLevel)})" : "";
                string chordTypeText = e.PlayRootOnly ? "Note" : $"Chord ({GetChordType(e)})";
                string velocityText = $" vel:{velocity}"; // Add velocity to log message

                LogChordActivity($"{chordTypeText} played: {rootNoteName}{inversionText}{velocityText} on device {deviceIndex}, channel {channel + 1}", true);
            }
            else
            {
                // Turn off all notes
                foreach (byte note in chordNotes)
                {
                    midiOutput.SendNoteOff(deviceIndex, channel, note);
                }

                LogChordActivity($"Chord released: {rootNoteName}", false);
            }
        }
        // Logs chord activity on Chord Mode logger ui on Chord Mode UI
        private void LogChordActivity(string message, bool isPlayed)
        {
            Dispatcher.Invoke(() =>
            {
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

        private string GetInversionName(int inversion)
        {
            return inversion switch
            {
                1 => "1st inversion",
                2 => "2nd inversion",
                3 => "3rd inversion",
                4 => "4th inversion",
                _ => "root position"
            };
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
        // ApplyInversion used on Chord Mode
        private List<byte> ApplyInversion(List<byte> chordNotes, int inversionLevel)
        {
            // No change needed for root position (inversionLevel = 0) or if we don't have enough notes
            if (inversionLevel == 0 || chordNotes.Count <= 1)
                return new List<byte>(chordNotes); // Return a copy of the list to avoid modifying the original

            // Make a copy of the notes to work with
            List<byte> invertedChord = new List<byte>(chordNotes);
            invertedChord.Sort(); // Ensure notes are in ascending order

            // Apply inversion (move lowest notes up by an octave)
            for (int i = 0; i < Math.Min(inversionLevel, invertedChord.Count); i++)
            {
                invertedChord[i] = (byte)(invertedChord[i] + 12); // Move up an octave
            }

            // Re-sort after inversion to get ascending order
            invertedChord.Sort();

            return invertedChord;
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
                TestChordRootCombo.Items.Clear();
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
        // Used by PlayCustomeChord_Click on Chord Mode UI
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
        // This seems to be serving double duty so be careful when refactoring Basic Mode and Chord Mode
        // When both the Basic tab and the chord tab were all part of mainwindow.xaml and xaml.cs, we were referencing a member of the basic tab in the chord tab, now that they are encapsulated we are in trouble because we are not able to reach basic tab combo boxes anymore. 
        private int GetSelectedMidiDeviceIndex()
        {
            // Use the ViewModel's selected device
            return ViewModel.SelectedMidiDeviceIndex;
        }
        // Used by DetermineChordName
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
        private void PopulateButtonNoteCombo(ComboBox? combo)
        {
            if (combo == null) return;

            combo.Items.Clear();

            // Use MidiNotes enum to ensure accuracy
            // Add notes for octaves 3, 4, and 5
            for (int octave = 3; octave <= 5; octave++)
            {
                // Add each note in this octave
                AddNoteToCombo(combo, "C", octave);
                AddNoteToCombo(combo, "C#", octave);
                AddNoteToCombo(combo, "D", octave);
                AddNoteToCombo(combo, "D#", octave);
                AddNoteToCombo(combo, "E", octave);
                AddNoteToCombo(combo, "F", octave);
                AddNoteToCombo(combo, "F#", octave);
                AddNoteToCombo(combo, "G", octave);
                AddNoteToCombo(combo, "G#", octave);
                AddNoteToCombo(combo, "A", octave);
                AddNoteToCombo(combo, "A#", octave);
                AddNoteToCombo(combo, "B", octave);
            }
        }

        private void AddNoteToCombo(ComboBox combo, string noteName, int octave)
        {
            // Get the correct MIDI note number using the enum
            int midiNote = GetMidiNoteNumber(noteName, octave);
            combo.Items.Add(new ComboBoxItem
            {
                Content = $"{noteName}{octave} ({midiNote})",
                Tag = midiNote
            });
        }
        // Clear chord Toggles on Chord Mode UI
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
        // Helper Function Used by Chord Sampler on Chord Mode UI
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

        private int GetMidiNoteNumber(string noteName, int octave)
        {
            // Use the enum values to get the correct MIDI note numbers
            string enumName = noteName.Replace("#", "Sharp") + octave;
            if (Enum.TryParse(enumName, out MidiNotes midiNote))
            {
                return (int)midiNote;
            }

            // Fallback calculation if the enum doesn't have the value
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int baseNote = (octave * 12) + Array.IndexOf(noteNames, noteName);
            return baseNote;
        }

    }

}