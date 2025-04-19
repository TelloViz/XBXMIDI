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
using XB2Midi.Utilities;

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

            // Subscribe to chord playback requests from the sampler component
            ChordSamplerView.ChordPlaybackRequested += OnChordPlaybackRequested;

            // Create the ViewModel
            ViewModel = new ChordMappingViewModel();

            // Set DataContext
            this.DataContext = ViewModel;

            // Connect the activity log
            ChordActivityLog.ItemsSource = ViewModel.ActivityLog;

        }

        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            this.midiOutput = output;
            this.mappingManager = mappingManager;
            
            // Add this crucial event subscription
            mappingManager.MappingsChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Update the UI controls that reflect mappings
                    // For chord mapping, we need to update the note combo boxes and channel/device selectors
                    UpdateButtonNoteComboBoxes();
                    UpdateChannelAndDeviceSelectors();
                });
            };

            // Register for mapping events
            mappingManager.RegisterMappingEventHandler(LogMidiEvent);

            // Initialize modeState with mappingManager
            modeState = new ModeState();
            modeState.ChordRequested += ModeState_ChordRequested;
            
            // Initialize mapping tab manager
            mappingTabManager.ActiveMappingChanged += MappingTabManager_ActiveMappingChanged;

            // Call the existing initialization method
            InitializeChordUI();
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

        /// <Summary>
        /// Handles controller input events and processes them based on the current mode state.
        /// </Summary>
        /// <param name="e">The event arguments containing the input data.</param>
        /// <remarks>
        /// This method is responsible for handling input from the controller and passing it to the appropriate handler.
        /// It also allows the mode state to handle chord-specific input.
        /// </remarks>
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            // Make sure we handle the input using the shared mappingManager
            if (mappingManager != null)
            {
                mappingManager.HandleControllerInput(e);
            }

            // Also allow the modeState to handle chord-specific input
            if (e.InputType == ControllerInputType.Button && modeState != null)
            {
                // Use the shoulder button states directly from the event args
                bool leftBumperHeld = e.IsLeftShoulderPressed;
                bool rightBumperHeld = e.IsRightShoulderPressed;

                // Process button input through chord handling
                modeState.HandleButtonInput(e.InputName, Convert.ToBoolean(e.Value), leftBumperHeld, rightBumperHeld);
            }
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
        private void MappingTabManager_ActiveMappingChanged(object sender, int newIndex)
        {


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

        private void ModeState_ChordRequested(object? sender, ChordEventArgs e)
        {
            if (midiOutput == null) return; // Check if MIDI output is available

            string rootNoteName = MusicTheory.GetNoteName(e.RootNote); // Get the root note name for logging

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

                string inversionText = inversionLevel > 0 ? $" ({MusicTheory.GetInversionName(inversionLevel)})" : "";  // Get inversion name for logging
                string chordTypeText = e.PlayRootOnly ? "Note" : $"Chord ({MusicTheory.GetChordType(e)})";              // Get chord type for logging
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
        /// Logs the chord activity to the UI and the main MIDI event log.
        /// </summary>
        /// <remarks>
        /// <i>This method adds the activity message to the UI's activity log and also logs it to the main MIDI event log.</i>
        /// </remarks>
        /// <param name="message"></param>
        /// <param name="isPlayed"></param>
        private void LogChordActivity(string message, bool isPlayed)
        {
            Dispatcher.Invoke(() =>
            {
                // Add to the bound collection in ViewModel
                ViewModel.ActivityLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");

                // Limit collection size
                while (ViewModel.ActivityLog.Count > 100)
                    ViewModel.ActivityLog.RemoveAt(ViewModel.ActivityLog.Count - 1);
            });

            LogMidiEvent(message);
        }

        /// <summary>
        /// Updates the channel and device selectors based on the current mode state.
        /// </summary>
        /// <remarks>
        /// <i>This method sets the selected index of the channel and device combo boxes
        /// for each button (A, B, X, Y, DPadUp, DPadRight, DPadDown, DPadLeft)
        /// based on the current mapping in the ModeState.
        /// It also handles the case where the combo boxes are null or the button name is not found in the mapping.</i>
/// </remarks>
        private void UpdateChannelAndDeviceSelectors()
        {
            var buttonNames = new[] { "A", "B", "X", "Y", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" }; // Define button names

            foreach (var buttonName in buttonNames) // Iterate through each button name
            {
                var channelCombo = this.FindName($"{buttonName}ChannelCombo") as ComboBox;  // Find the channel combo box by name
                var deviceCombo = this.FindName($"{buttonName}DeviceCombo") as ComboBox;    // Find the device combo box by name

                if (channelCombo != null && modeState?.ButtonChannelMap != null &&
                    modeState.ButtonChannelMap.TryGetValue(buttonName, out byte channel))   // Try to get the channel from the mapping
                {
                    channelCombo.SelectedIndex = channel;                 // Set the selected index of the channel combo box
                }

                if (deviceCombo != null && modeState?.ButtonDeviceMap != null &&
                    modeState.ButtonDeviceMap.TryGetValue(buttonName, out int deviceIndex)) // Try to get the device index from the mapping
                {
                    if (deviceIndex < deviceCombo.Items.Count)           // Check if the device index is valid
                        deviceCombo.SelectedIndex = deviceIndex;        // Set the selected index of the device combo box
                }
            }
        }

        /// <summary>
        /// Applies inversion to the chord notes based on the specified inversion level.
        /// </summary>
        /// <remarks>
        /// <i>This method takes a list of chord notes and an inversion level,
        /// and applies the inversion by moving the lowest notes up by an octave.
        /// It returns a new list of inverted chord notes.</i>
        /// </remarks>
        /// <param name="chordNotes"></param>
        /// <param name="inversionLevel"></param>
        /// <returns>
        /// A new list of inverted chord notes.
        /// </returns>
        private List<byte> ApplyInversion(List<byte> chordNotes, int inversionLevel)
        {
            if (inversionLevel == 0 || chordNotes.Count <= 1)                           // No inversion needed for root position or single note
                return new List<byte>(chordNotes);                                      // Return a copy of the original notes

            List<byte> invertedChord = new List<byte>(chordNotes);                      // Create a copy of the original notes
            invertedChord.Sort();                                                       // Sort the notes in ascending order

            for (int i = 0; i < Math.Min(inversionLevel, invertedChord.Count); i++)     // Apply inversion to the lowest notes
            {
                invertedChord[i] = (byte)(invertedChord[i] + 12);                       // Move the note up by an octave
            }

            invertedChord.Sort();                                                       // Sort the inverted chord notes again

            return invertedChord;                                                       // Return the inverted chord notes
        }

        /// <summary>
        /// Populates the note combo boxes with available MIDI notes.
        /// </summary>
        /// <remarks>
        /// <i>This method populates the note combo boxes for button mapping with available MIDI notes.
        /// It creates a list of note names and adds them to the combo boxes for octaves 2 to 6.
        /// It also sets the default selected index for the test chord root note combo box.</i>
/// </remarks>
        private void PopulateNoteComboBoxes()
        {
            var noteNames = new List<string>    // Define an array of note names
            {
                "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
            };

            PopulateButtonNoteCombo(AButtonNoteCombo);                 // Populate A button note combo box
            PopulateButtonNoteCombo(BButtonNoteCombo);                  // Populate B button note combo box
            PopulateButtonNoteCombo(XButtonNoteCombo);                  // Populate X button note combo box
            PopulateButtonNoteCombo(YButtonNoteCombo);                  // Populate Y button note combo box
            PopulateButtonNoteCombo(DPadUpNoteCombo);                   // Populate DPadUp button note combo box
            PopulateButtonNoteCombo(DPadDownNoteCombo);                 // Populate DPadDown button note combo box
            PopulateButtonNoteCombo(DPadLeftNoteCombo);                 // Populate DPadLeft button note combo box
            PopulateButtonNoteCombo(DPadRightNoteCombo);                // Populate DPadRight button note combo box
        }

        /// <summary>
        /// Populates the button note combo box with available MIDI notes.
        /// </summary>
        /// <remarks>
        /// <i>This method populates the given combo box with available MIDI notes for octaves 3, 4, and 5.
        /// It uses the MidiNotes enum to ensure accuracy and adds each note with its corresponding MIDI note number.</i>
        /// </remarks>
        /// <param name="combo"></param>
        private void PopulateButtonNoteCombo(ComboBox? combo)
        {
            if (combo == null) return;      // Check if combo box is null

            combo.Items.Clear();        // Clear existing items

            for (int octave = 3; octave <= 5; octave++)     // Iterate through octaves 3 to 5
            {
                AddNoteToCombo(combo, "C", octave);         // Add C note to combobox
                AddNoteToCombo(combo, "C#", octave);        // Add C# note
                AddNoteToCombo(combo, "D", octave);         // Add D note
                AddNoteToCombo(combo, "D#", octave);        // Add D# note
                AddNoteToCombo(combo, "E", octave);         // Add E note
                AddNoteToCombo(combo, "F", octave);         // Add F note
                AddNoteToCombo(combo, "F#", octave);        // Add F# note
                AddNoteToCombo(combo, "G", octave);         // Add G note
                AddNoteToCombo(combo, "G#", octave);        // Add G# note
                AddNoteToCombo(combo, "A", octave);         // Add A note
                AddNoteToCombo(combo, "A#", octave);        // Add A# note
                AddNoteToCombo(combo, "B", octave);         // Add B note
            }
        }

        /// <summary>
        /// Adds a note to the given combo box with its MIDI note number.
        /// </summary>
        /// <remarks>
        /// <i>This method creates a ComboBoxItem with the note name and MIDI note number,
        /// and adds it to the specified combo box.
        /// It uses the GetMidiNoteNumber method to get the correct MIDI note number.</i>
        /// </remarks>
        /// <param name="combo"></param>
        /// <param name="noteName"></param>
        /// <param name="octave"></param>
        private void AddNoteToCombo(ComboBox combo, string noteName, int octave)
        {
            int midiNote = GetMidiNoteNumber(noteName, octave);     // Get the MIDI note number using the enum values
            combo.Items.Add(new ComboBoxItem                        // Create a new ComboBoxItem with the note name and MIDI note number
            {
                Content = $"{noteName}{octave} ({midiNote})",       // Display note name and MIDI note number
                Tag = midiNote
            });
        }

        /// <summary>
        /// Gets the MIDI note number based on the note name and octave.
        /// </summary>
        /// <remarks>
        /// <i>This method takes a note name (e.g., C, D#, etc.) and an octave number,
        /// and returns the corresponding MIDI note number.
        /// It uses the MidiNotes enum to ensure accuracy.</i>
        /// </remarks>
        /// <param name="noteName"></param>
        /// <param name="octave"></param>
        /// <returns>
        /// The MIDI note number as an integer (0-127).
        /// </returns>
        private int GetMidiNoteNumber(string noteName, int octave)
        {
            string enumName = noteName.Replace("#", "Sharp") + octave;  // Create the enum name from note name and octave
            if (Enum.TryParse(enumName, out MidiNotes midiNote))        // Try to parse the enum name
            {
                return (int)midiNote;
            }

            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };   // Array of note names
            int baseNote = (octave * 12) + Array.IndexOf(noteNames, noteName);                          // Calculate the base note number
            return baseNote;                                // Return the MIDI note number
        }

        // Method to handle chord playback requests from ViewModel
        private void OnChordPlaybackRequested(object sender, ChordPlaybackEventArgs e)
        {
            if (midiOutput == null) return;
            
            List<byte> chordNotes = e.ChordNotes;
            
            // Skip if no notes are selected
            if (chordNotes.Count == 0)
                return;
            
            // Apply inversion if specified
            if (e.InversionLevel > 0)
            {
                chordNotes = ApplyInversion(chordNotes, e.InversionLevel);
            }
            
            // Play the chord
            int deviceIndex = ViewModel.SelectedMidiDeviceIndex;
            byte velocity = 100;
            
            // Send note-on for all notes in the chord
            foreach (byte note in chordNotes)
            {
                midiOutput.SendNoteOn(deviceIndex, 0, note, velocity);
            }
            
            // Generate chord name for logging
            string chordName = MusicTheory.DetermineChordName(chordNotes, e.RootNote);
            
            // Add inversion information to the log message
            string inversionText = e.InversionLevel == 0 ? "" : $" ({MusicTheory.GetInversionName(e.InversionLevel)})";
            
            LogChordActivity($"Custom chord played: {MusicTheory.GetNoteName(e.RootNote)} {chordName}{inversionText}", true);
            
            // Schedule note-off after 500ms
            Task.Delay(500).ContinueWith(_ =>
            {
                foreach (byte note in chordNotes)
                {
                    midiOutput.SendNoteOff(deviceIndex, 0, note);
                }
            });
        }
    }

}