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

    /// <summary>
    /// Interaction logic for ChordMappingView.xaml
    /// This class handles the UI and logic for the Chord Mapping feature in the application.
    /// </summary>
    /// <remarks>
    /// <i>Also see ChordMappingViewModel.cs for the ViewModel that handles the data and logic behind the UI.</i>
    /// </remarks>
    public partial class ChordMappingView : UserControl
    {
        public ChordMappingViewModel ViewModel { get; private set; } // ViewModel instance for data binding


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

        /// <summary>
        /// Handles Click event for preset buttons in the Chord Sampler UI.
        /// This method sets the appropriate note toggles based on the selected preset.
        /// It also plays the chord immediately after setting the toggles.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChordPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button) // Check if the sender is a Button
            {
                ClearChordToggles(); // Clear all toggles

                RootToggle.IsChecked = true; // Set root toggle to true

                // Configure the chord based on preset
                switch (button.Content.ToString()) // Get the button content as string
                {
                    case "Major": // Major chord preset
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        break;

                    case "Minor": // Minor chord preset
                        MinThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        break;

                    case "Maj7": // Major 7th chord preset
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        MajSeventhToggle.IsChecked = true;
                        break;

                    case "Min7": // Minor 7th chord preset
                        MinThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        DomSeventhToggle.IsChecked = true;
                        break;

                    case "Dom7": // Dominant 7th chord preset
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        DomSeventhToggle.IsChecked = true;
                        break;

                    case "Dim": // Diminished chord preset
                        MinThirdToggle.IsChecked = true;
                        FlatFifthToggle.IsChecked = true;
                        break;

                    case "Sus4": // Suspended 4th chord preset
                        // In Sus4, we omit the third and add a fourth
                        MajThirdToggle.IsChecked = false;
                        MinThirdToggle.IsChecked = false;
                        FifthToggle.IsChecked = true;
                        break;

                    case "Add9": // Added 9th chord preset
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        NinthToggle.IsChecked = true;
                        break;

                    case "6": // Major 6th chord preset
                        MajThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        SixthToggle.IsChecked = true;
                        break;

                    case "m6": // Minor 6th chord preset
                        MinThirdToggle.IsChecked = true;
                        FifthToggle.IsChecked = true;
                        SixthToggle.IsChecked = true;
                        break;
                }

                PlayCustomChord_Click(sender, e); // Call the method to play the chord
            }
        }

        /// <summary>
        /// Handles the click event for note toggles in the Chord Sampler UI.
        /// This method ensures that only one toggle is selected at a time for each group of notes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NoteToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton clickedButton) // Check if the sender is a ToggleButton
            {
                // Handle exclusive toggling between major and minor third
                if (clickedButton == MajThirdToggle && clickedButton.IsChecked == true) // If Major third is checked
                {
                    MinThirdToggle.IsChecked = false; // Uncheck Minor third
                }
                else if (clickedButton == MinThirdToggle && clickedButton.IsChecked == true) // If Minor third is checked
                {
                    MajThirdToggle.IsChecked = false; // Uncheck Major third
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

        /// <summary>
        /// Handles the click event for the "Clear Chord" button in the Chord Sampler UI.
        /// This method clears all the toggles for the chord notes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearChord_Click(object sender, RoutedEventArgs e)
        {
            ClearChordToggles();
        }

        /// <summary>
        /// Plays a custom chord based on the selected root note 
        /// and toggled notes in the Chord Sampler UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayCustomChord_Click(object sender, RoutedEventArgs e)
        {
            if (midiOutput == null || TestChordRootCombo?.SelectedItem == null) return; // Check if midiOutput is null or root note is not selected

            // Get the root note
            string noteText = TestChordRootCombo.SelectedItem.ToString() ?? "C4"; // Get the selected note text or default to "C4"
            byte rootNote = GetMidiNoteFromName(noteText); // Convert note name to MIDI note number

            // Create a list to hold all the notes in our chord
            List<byte> chordNotes = new List<byte>(); // List to hold the notes of the chord

            // Add root note only if toggled on
            if (RootToggle.IsChecked == true) // Check if root toggle is checked
                chordNotes.Add(rootNote); // Add root note to the chord

            // Add other notes based on toggles
            if (MajThirdToggle.IsChecked == true) // Check if Major third toggle is checked
                chordNotes.Add((byte)(rootNote + 4)); // Major 3rd

            if (MinThirdToggle.IsChecked == true)
                chordNotes.Add((byte)(rootNote + 3)); // Minor 3rd

            // Special case for Sus4
            if (!MajThirdToggle.IsChecked == true && !MinThirdToggle.IsChecked == true) // If neither Major nor Minor third is checked
                if (FifthToggle.IsChecked == true || FlatFifthToggle.IsChecked == true) // Check if either Fifth or Flat fifth is checked
                    chordNotes.Add((byte)(rootNote + 5)); // Perfect 4th (for sus4 chord)

            if (FifthToggle.IsChecked == true) // Check if Fifth toggle is checked
                chordNotes.Add((byte)(rootNote + 7)); // Perfect 5th

            if (FlatFifthToggle.IsChecked == true) // Check if Flat fifth toggle is checked
                chordNotes.Add((byte)(rootNote + 6)); // Diminished 5th

            if (SixthToggle.IsChecked == true) // Check if Major 6th toggle is checked
                chordNotes.Add((byte)(rootNote + 9)); // Major 6th

            if (DomSeventhToggle.IsChecked == true) // Check if Dominant 7th toggle is checked
                chordNotes.Add((byte)(rootNote + 10)); // Dominant 7th (minor 7th)

            if (MajSeventhToggle.IsChecked == true) // Check if Major 7th toggle is checked
                chordNotes.Add((byte)(rootNote + 11)); // Major 7th

            if (NinthToggle.IsChecked == true) // Check if Major 9th toggle is checked
                chordNotes.Add((byte)(rootNote + 14)); // Major 9th

            if (FlatNinthToggle.IsChecked == true) // Check if Flat 9th toggle is checked
                chordNotes.Add((byte)(rootNote + 13)); // Flat 9th

            // Skip if no notes are selected
            if (chordNotes.Count == 0) // Check if no notes are selected
                return;

            // Apply inversion if selected
            int inversionLevel = 0; // Default inversion level
            if (ChordInversionCombo?.SelectedItem is ComboBoxItem inversionItem && inversionItem.Tag is int level) // Check if inversion item is selected and has a valid tag
            {
                inversionLevel = level; // Get the inversion level from the selected item
                chordNotes = ApplyInversion(chordNotes, inversionLevel); // Apply inversion to the chord notes
            }

            // Play the chord
            int deviceIndex = GetSelectedMidiDeviceIndex(); // Get the selected MIDI device index
            byte velocity = 100; // Set the velocity for note-on messages

            // Send note-on for all notes in the chord
            foreach (byte note in chordNotes) // Iterate through all notes in the chord
            {
                midiOutput.SendNoteOn(deviceIndex, 0, note, velocity); // Send note-on message for each note
            }

            // Generate chord name for logging
            string chordName = DetermineChordName(chordNotes, rootNote); // Get the chord name based on the notes

            // Add inversion information to the log message
            string inversionText = inversionLevel == 0 ? "" : // Check if inversion level is 0
                $" ({((ChordInversionCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? $"{inversionLevel} inversion")})"; // Get inversion text from the selected item or use default

            LogChordActivity($"Custom chord played: {GetNoteName(rootNote)} {chordName}{inversionText}", true); // Log the chord activity

            // Schedule note-off after 500ms
            Task.Delay(500).ContinueWith(_ => // Schedule note-off after 500ms
            {
                foreach (byte note in chordNotes) // Iterate through all notes in the chord
                {
                    midiOutput.SendNoteOff(deviceIndex, 0, note); // Send note-off message for each note
                }
            });
        }

        
        /// <summary>
        /// Initializes the UI components for the Chord Mode.
        /// This method populates note selection combos, initializes mapping tabs,
        /// updates button note mapping combos, and subscribes to events.
        /// It also ensures that the ViewModel is updated with MIDI devices.
        /// </summary>
        private void InitializeChordModeUI()
        {
            PopulateNoteComboBoxes(); // Populate note selection combos

            InitializeChordMappingTabs(); // Initialize mapping tabs

            UpdateButtonNoteComboBoxes(); // Update button note mapping combos

            modeState.ChordRequested += ModeState_ChordRequested; // Subscribe to chord requested event from ModeState

            PopulateChannelAndDeviceSelectors(); // Populate channel and device options for each button

            PopulateChordInversionComboBox(); // Populate chord inversion combo box
        }

        /// <summary>
        /// Initializes the mapping tabs for the Chord Mode UI.
        /// This method sets up the tabs for different chord mappings,
        /// subscribes to events, and applies the initial mapping.
        /// </summary>
        private void InitializeChordMappingTabs()
        {
            mappingTabManager.ActiveMappingChanged += MappingTabManager_ActiveMappingChanged; // Subscribe to active mapping changed event

            RefreshMappingTabs(); // Refresh the mapping tabs in the UI

            mappingTabManager.ApplyMapping(0, modeState); // Apply the initial mapping to the mode state
        }

        /// <summary>
        /// Refreshes the mapping tabs in the UI.
        /// This method clears existing tabs and adds new ones based on the current mappings.
        /// </summary>
        private void RefreshMappingTabs()
        {
            int currentIndex = MappingTabsControl.SelectedIndex; // Get the current selected index

            MappingTabsControl.Items.Clear(); // Clear existing tabs

            for (int i = 0; i < mappingTabManager.ChordMappings.Count; i++) // Iterate through all mappings
            {
                var mapping = mappingTabManager.ChordMappings[i]; // Get the current mapping

                var tabItem = new TabItem // Create a new tab item
                {
                    Header = CreateMappingTabHeader(mapping.Name, i), // Set the header to the mapping name
                    Tag = i // Set the tag to the mapping index
                };

                MappingTabsControl.Items.Add(tabItem); // Add the tab item to the control
            }

            if (mappingTabManager.CanAddMapping) // Check if we can add a new mapping
            {
                var addTab = new TabItem // Create a new tab item for adding a mapping
                {
                    Header = "+", // Set the header to "+"
                    Tag = -1 // Set the tag to -1 to indicate it's the add tab
                };

                MappingTabsControl.Items.Add(addTab); // Add the add tab to the control
            }

            if (currentIndex >= 0 && currentIndex < MappingTabsControl.Items.Count - 1) // Check if the current index is valid
            {
                MappingTabsControl.SelectedIndex = currentIndex; // Set the selected index to the current index
            }
            else
            {
                MappingTabsControl.SelectedIndex = Math.Min(mappingTabManager.ActiveMappingIndex, MappingTabsControl.Items.Count - 2); // Set the selected index to the active mapping index or the last valid index
            }
        }

        /// <summary>
        /// Creates the header for a mapping tab in the UI.
        /// This method sets up the layout and adds a close button if necessary.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private object CreateMappingTabHeader(string name, int index)
        {
            var panel = new DockPanel(); // Create a new DockPanel for layout

            var textBlock = new TextBlock { Text = name, Margin = new Thickness(0, 0, 5, 0) }; // Create a TextBlock for the mapping name
            DockPanel.SetDock(textBlock, Dock.Left); // Set the dock position to left
            panel.Children.Add(textBlock); // Add the TextBlock to the panel

            if (mappingTabManager.ChordMappings.Count > 1 && index >= 0) // Check if we have more than one mapping and the index is valid
            {
                var closeButton = new Button // Create a new Button for closing the tab
                {
                    Content = "×",                                          // Set the content to "×"
                    Padding = new Thickness(2, 0, 2, 1),                    // Set padding for the button
                    Margin = new Thickness(0),                              // Set margin for the button
                    FontSize = 10,                                          // Set font size for the button
                    VerticalAlignment = VerticalAlignment.Top,
                    VerticalContentAlignment = VerticalAlignment.Center,    // Set vertical content alignment
                    BorderThickness = new Thickness(0),                     // Set border thickness to 0
                    Background = Brushes.Transparent,                       // Set background to transparent
                    Foreground = Brushes.Gray,                              // Set foreground color to gray
                    Tag = index                                             // Set the tag to the mapping index
                };

                closeButton.Click += CloseTab_Click;                        // Subscribe to the click event for closing the tab
                DockPanel.SetDock(closeButton, Dock.Right);                 // Set the dock position to right
                panel.Children.Add(closeButton);                            // Add the close button to the panel
            }

            return panel;                                                   // Return the panel as the header content
        }

        /// <summary>
        /// Handles the click event for the close button on a mapping tab.
        /// This method removes the mapping from the manager and refreshes the tabs.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int tabIndex) // Check if the sender is a Button and has a valid tag
            {
                e.Handled = true; // Mark the event as handled

                if (mappingTabManager.RemoveMapping(tabIndex)) // Try to remove the mapping from the manager
                {
                    RefreshMappingTabs(); // Refresh the mapping tabs in the UI
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

        /// <summary>
        /// Handles the click event for the "Save Chord Mappings" button.
        /// This method saves the current chord mappings to a file.
        /// It first updates the active mapping with the current state,
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
         private void SaveChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (mappingManager == null) return; // Check if mapping manager is null

            try
            {
                mappingTabManager.UpdateMappingFromState(mappingTabManager.ActiveMappingIndex, modeState); // Update the active mapping with the current state

                foreach (var mapping in mappingTabManager.ChordMappings) // Iterate through all mappings
                {
                    mappingManager.SaveChordMapping(mapping); // Save each mapping
                }

                var dialog = new Microsoft.Win32.SaveFileDialog // Create a SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",      // Set the filter for file types
                    DefaultExt = ".json",                                           // Set the default extension
                    Title = "Save Chord Mappings"                                   // Set the title of the dialog
                };

                if (dialog.ShowDialog() == true)    // Show the dialog and check if the user clicked OK
                {
                    // Save to file
                    mappingManager.SaveMappings(dialog.FileName);                       // Save the mappings to the selected file
                    LogMidiEvent($"Chord mappings saved to {dialog.FileName}");         // Log the save action         
                    MessageBox.Show("Chord mappings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information); // Show success message
                }
            }
            catch (Exception ex) // Catch any exceptions that occur during the save process
            {
                MessageBox.Show($"Error saving chord mappings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); // Show error message
            }
        }

        /// <summary>
        /// Handles the click event for the "Load Chord Mappings" button. 
        /// This method allows the user to select a file and load chord mappings from it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
       private void LoadChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (mappingManager == null) return; // Check if mapping manager is null

            try
            {
                // Ask user to select a file
                var dialog = new Microsoft.Win32.OpenFileDialog                     // Create an OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",      // Set the filter for file types
                    DefaultExt = ".json",                                           // Set the default extension
                    Title = "Load Chord Mappings"                                   // Set the title of the dialog
                };

                if (dialog.ShowDialog() == true)                                    // Show the dialog and check if the user clicked OK
                {

                    mappingManager.LoadMappings(dialog.FileName);                   // Load mappings from the selected file

                    if (mappingManager.LoadChordMapping(modeState))                 // Load the chord mapping into the current mode state
                    {
                        UpdateButtonNoteComboBoxes();                               // Update the button note combo boxes to reflect the loaded mapping

                        UpdateChannelAndDeviceSelectors();                          // Update the channel and device selectors to reflect the loaded mapping

                        LogMidiEvent($"Chord mappings loaded from {dialog.FileName}");             // Log the load action
                        MessageBox.Show("Chord mappings loaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information); // Show success message
                    }
                    else
                    {
                        LogMidiEvent("No chord mappings found in the selected file."); // Log the error
                        MessageBox.Show("No chord mappings found in the selected file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning); // Show warning message
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading chord mappings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); // Show error message
            }
        }


        /// <summary>
        /// Handles the click event for the "Reset Chord Mappings" button.
        /// This method resets the current chord mappings to their default state.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetChordMappings_Click(object sender, RoutedEventArgs e)
        {
            if (modeState != null && mappingTabManager.ActiveMapping != null) // Check if mode state and active mapping are not null
            {
                string currentName = mappingTabManager.ActiveMapping.Name; // Get the current name of the mapping

                modeState.ResetButtonMappings(); // Reset button mappings to default values

                mappingTabManager.UpdateMappingFromState(mappingTabManager.ActiveMappingIndex, modeState); // Update the active mapping with the current state

                mappingTabManager.RenameMappingAt(mappingTabManager.ActiveMappingIndex, currentName); // Rename the mapping to its original name

                UpdateButtonNoteComboBoxes(); // Update the button note combo boxes to reflect the reset mapping
                UpdateChannelAndDeviceSelectors(); // Update the channel and device selectors to reflect the reset mapping

                LogMidiEvent("Chord mapping reset to defaults"); // Log the reset action
            }
        }

        /// <summary>
        /// Populates the chord inversion combo box with available inversions.
        /// This method adds items for root position, 1st inversion, 2nd inversion,
        /// </summary>
        private void PopulateChordInversionComboBox()
        {
            if (ChordInversionCombo != null) // Check if the combo box is not null
            {
                ChordInversionCombo.Items.Clear(); // Clear existing items
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "Root Position", Tag = 0 }); // Add root position item
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "1st Inversion", Tag = 1 }); // Add 1st inversion item
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "2nd Inversion", Tag = 2 }); // Add 2nd inversion item
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "3rd Inversion", Tag = 3 }); // Add 3rd inversion item
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "4th Inversion", Tag = 4 }); // Add 4th inversion item
                ChordInversionCombo.SelectedIndex = 0; // Default to Root Position
            }
        }

        /// <summary>
        /// Updates the button note combo boxes based on the current mapping.
        /// This method sets the selected item in each combo box according to the
        /// current mapping in the ModeState. It also adds change handlers to update
        /// the mapping when the user selects a different note from the combo box.
        /// </summary>
        /// <remarks>
        /// <b>This method is called during the initialization of the Chord Mode UI
        /// and is specific to the Chord Mode UI due to the use of specific button names.</b>
        /// </remarks>
        private void UpdateButtonNoteComboBoxes()
        {
            // Set comboboxes according to current mapping
            UpdateButtonNoteCombo(AButtonNoteCombo, "A");                   // Update A button note combo
            UpdateButtonNoteCombo(BButtonNoteCombo, "B");                   // Update B button note combo
            UpdateButtonNoteCombo(XButtonNoteCombo, "X");                   // Update X button note combo
            UpdateButtonNoteCombo(YButtonNoteCombo, "Y");                   // Update Y button note combo
            UpdateButtonNoteCombo(DPadUpNoteCombo, "DPadUp");               // Update DPadUp button note combo
            UpdateButtonNoteCombo(DPadDownNoteCombo, "DPadDown");           // Update DPadDown button note combo
            UpdateButtonNoteCombo(DPadLeftNoteCombo, "DPadLeft");           // Update DPadLeft button note combo
            UpdateButtonNoteCombo(DPadRightNoteCombo, "DPadRight");         // Update DPadRight button note combo

            // Add change handlers
            AddNoteComboChangeHandler(AButtonNoteCombo, "A");               // Add change handler for A button note combo
            AddNoteComboChangeHandler(BButtonNoteCombo, "B");               // Add change handler for B button note combo
            AddNoteComboChangeHandler(XButtonNoteCombo, "X");               // Add change handler for X button note combo
            AddNoteComboChangeHandler(YButtonNoteCombo, "Y");               // Add change handler for Y button note combo
            AddNoteComboChangeHandler(DPadUpNoteCombo, "DPadUp");           // Add change handler for DPadUp button note combo
            AddNoteComboChangeHandler(DPadDownNoteCombo, "DPadDown");       // Add change handler for DPadDown button note combo
            AddNoteComboChangeHandler(DPadLeftNoteCombo, "DPadLeft");       // Add change handler for DPadLeft button note combo
            AddNoteComboChangeHandler(DPadRightNoteCombo, "DPadRight");     // Add change handler for DPadRight button note combo
        }

        /// <summary>
        /// Updates the selected item in the given combo box based on the current mapping.
        /// 
        /// </summary>
        /// <remarks>
        /// <i>This method looks up the note value in the ModeState's button note map 
        /// and sets the selected item in the combo box accordingly.
        /// It also handles the case where the combo box is null or 
        /// the button name is not found in the mapping.</i>
        /// </remarks>
        /// <param name="combo"></param>
        /// <param name="buttonName"></param>
        private void UpdateButtonNoteCombo(ComboBox? combo, string buttonName)
        {
            if (combo == null || modeState?.ButtonNoteMap == null) return;              // Check if combo box is null or button note map is null

            if (modeState.ButtonNoteMap.TryGetValue(buttonName, out byte noteValue))    // Try to get the note value from the mapping
            {
                foreach (ComboBoxItem item in combo.Items)                              // Iterate through all items in the combo box
                {
                    if (item.Tag is int midiNote && midiNote == noteValue)              // Check if the item tag is a valid MIDI note
                    {
                        combo.SelectedItem = item;                                      // Set the selected item to the matching item
                        break;
                    }
                }
            }
        }


        /// <summary>
        /// Adds a change handler to the given combo box for note selection.
        /// </summary>
        /// <remarks>
        /// <i>This method subscribes to the SelectionChanged event of the combo box
        /// and updates the button note mapping in the ModeState when the user selects a new note.</i>
        /// </remarks>
        /// <param name="combo"></param>
        /// <param name="buttonName"></param>
        private void AddNoteComboChangeHandler(ComboBox? combo, string buttonName)
        {
            if (combo == null) return;  // Check if combo box is null

            combo.SelectionChanged += (s, e) => // Subscribe to the SelectionChanged event
            {
                if (combo.SelectedItem is ComboBoxItem selected && selected.Tag is int midiNote) // Check if the selected item is a ComboBoxItem and has a valid MIDI note tag
                {
                    modeState.ButtonNoteMap[buttonName] = (byte)midiNote; // Update the button note mapping in the ModeState
                    LogMidiEvent($"Updated {buttonName} button note mapping to {selected.Content}"); // Log the update action
                }
            };
        }

        /// <summary>
        /// Populates the channel and device selectors for each button in the UI.
        /// </summary>
        /// <remarks>
        /// <i>This method populates the channel and device combo boxes for each button (A, B, X, Y, DPadUp, DPadRight, DPadDown, DPadLeft)
        /// with available MIDI channels and devices.
        /// It also sets the initial selection based on the current mapping in the ModeState.
        /// It adds change handlers to update the mapping when the user selects a different channel or device.</i>
        /// </remarks>
        private void PopulateChannelAndDeviceSelectors()
        {
            var buttonNames = new[] { "A", "B", "X", "Y", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" }; // Define button names

            foreach (var buttonName in buttonNames) // Iterate through each button name
            {
                var channelCombo = this.FindName($"{buttonName}ChannelCombo") as ComboBox; // Find the channel combo box by name
                var deviceCombo = this.FindName($"{buttonName}DeviceCombo") as ComboBox;    // Find the device combo box by name

                if (channelCombo != null) // Check if the channel combo box is not null
                {
                    for (int i = 1; i <= 16; i++) // Iterate through MIDI channels 1 to 16
                    {
                        channelCombo.Items.Add(i); // Add channel numbers to the combo box
                    }

                    byte channel = 0; // Default channel to 1 (0-based index)
                    if (modeState.ButtonChannelMap.TryGetValue(buttonName, out channel)) // Try to get the channel from the mapping
                    {
                        channelCombo.SelectedIndex = channel; // Select the appropriate channel (0-based)
                    }
                    else
                    {
                        channelCombo.SelectedIndex = 0; // Default to channel 1
                    }

                    channelCombo.SelectionChanged += (s, e) =>
                    {
                        if (channelCombo.SelectedIndex >= 0) // Check if a valid channel is selected
                        {
                            byte selectedChannel = (byte)channelCombo.SelectedIndex; // Get the selected channel (0-based index)
                            modeState.ButtonChannelMap[buttonName] = selectedChannel; // Update the button channel mapping in the ModeState
                            LogMidiEvent($"Updated {buttonName} button MIDI channel to {selectedChannel + 1}"); // Log the update action
                        }
                    };
                }

                if (deviceCombo != null) // Check if the device combo box is not null
                {
                    for (int i = 0; i < MidiOut.NumberOfDevices; i++) // Iterate through available MIDI devices
                    {
                        deviceCombo.Items.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}"); // Add device names to the combo box
                    }

                    int deviceIndex = 0; // Default device index to 0
                    if (modeState.ButtonDeviceMap.TryGetValue(buttonName, out deviceIndex)) // Try to get the device index from the mapping
                    {
                        if (deviceIndex < deviceCombo.Items.Count) // Check if the device index is valid
                            deviceCombo.SelectedIndex = deviceIndex; // Select the appropriate device
                        else
                            deviceCombo.SelectedIndex = 0; // Default to the first device if index is out of range
                    }
                    else
                    {
                        deviceCombo.SelectedIndex = 0; // Default to the first device
                    }

                    deviceCombo.SelectionChanged += (s, e) => // Subscribe to the SelectionChanged event
                    {
                        if (deviceCombo.SelectedIndex >= 0) // Check if a valid device is selected
                        {
                            modeState.ButtonDeviceMap[buttonName] = deviceCombo.SelectedIndex; // Update the button device mapping in the ModeState
                            string deviceName = deviceCombo.SelectedItem.ToString() ?? ""; // Get the selected device name
                            LogMidiEvent($"Updated {buttonName} button MIDI device to {deviceName}"); // Log the update action
                        }
                    };
                }
            }
        }


        /// <summary>
        /// Handles the ChordRequested event from the ModeState.
        /// </summary>
        /// <remarks>
        /// <i>This method is called when a chord is requested to be played or released.
        /// It sends MIDI Note On/Off messages to the specified device and channel.
        /// It also logs the chord activity to the UI and the main MIDI event log.</i>
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeState_ChordRequested(object? sender, ChordEventArgs e)
        {
            if (midiOutput == null) return; // Check if MIDI output is available

            string rootNoteName = GetNoteName(e.RootNote); // Get the root note name for logging

            byte channel = e.Channel; // Get the MIDI channel from the event args
            int deviceIndex = e.DeviceIndex; // Get the device index from the event args

            byte velocity = e.IsOn ? modeState.GetCurrentVelocity() : (byte)0; // Get the velocity from the ModeState (0 if not playing)

            int inversionLevel = e.InversionLevel;          // Get the inversion level from the event args

            List<byte> chordNotes = new List<byte>();       // List to hold the notes of the chord

            chordNotes.Add(e.RootNote);                     // Add the root note to the chord notes

            if (!e.PlayRootOnly)                            // Check if we should play the full chord
            {
                chordNotes.Add(e.ThirdNote);                // Add the third note to the chord notes
                chordNotes.Add(e.FifthNote);                // Add the fifth note to the chord notes

                if (e.HasSeventh)                           // Check if the chord has a seventh note
                    chordNotes.Add(e.SeventhNote);          // Add the seventh note to the chord notes

                if (e.HasNinth)                             // Check if the chord has a ninth note
                    chordNotes.Add(e.NinthNote);

                if (inversionLevel > 0)                     // Check if we need to apply inversion
                {
                    var originalNotes = new List<byte>(chordNotes);          // Make a copy of the original notes for logging
                    chordNotes = ApplyInversion(chordNotes, inversionLevel); // Apply inversion to the chord notes
                }
            }

            if (e.IsOn) // Check if we are playing the chord
            {
                foreach (byte note in chordNotes) // Iterate through the chord notes
                {
                    midiOutput.SendNoteOn(deviceIndex, channel, note, velocity); // Send Note On message for each note
                }

                string inversionText = inversionLevel > 0 ? $" ({GetInversionName(inversionLevel)})" : "";  // Get inversion name for logging
                string chordTypeText = e.PlayRootOnly ? "Note" : $"Chord ({GetChordType(e)})";              // Get chord type for logging
                string velocityText = $" vel:{velocity}";                                                   // Get velocity for logging

                LogChordActivity($"{chordTypeText} played: {rootNoteName}{inversionText}{velocityText} on device {deviceIndex}, channel {channel + 1}", true); // Log the chord activity
            }
            else // Check if we are releasing the chord
            {
                foreach (byte note in chordNotes)                           // Iterate through the chord notes
                {
                    midiOutput.SendNoteOff(deviceIndex, channel, note);     // Send Note Off message for each note
                }

                LogChordActivity($"Chord released: {rootNoteName}", false); // Log the chord release activity
            }
        }

        /// <summary>
        /// Logs chord activity to the UI and the main MIDI event log.
        /// </summary>
        /// <remarks>
        /// <i>This method updates the ChordActivityLog in the UI with the provided message.
        /// It also logs the activity to the main MIDI event log.</i>
        /// </remarks>
        /// <param name="message"></param>
        /// <param name="isPlayed"></param>
        /// <returns></returns>
        private void LogChordActivity(string message, bool isPlayed)
        {
            Dispatcher.Invoke(() => // Use Dispatcher to update UI elements from the UI thread
            {
                if (ChordActivityLog != null) // Check if the ChordActivityLog is not null
                {
                    ChordActivityLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");   // Insert the message at the top of the log
                    if (ChordActivityLog.Items.Count > 100)                                         // Limit the log to 100 items
                        ChordActivityLog.Items.RemoveAt(ChordActivityLog.Items.Count - 1);          // Remove the oldest item if the limit is exceeded
                }
            });

            LogMidiEvent(message); // Log the message to the main MIDI event log
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