using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Diagnostics;
using XB2Midi.Models;
using XB2Midi.ViewModels;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for MultiMappingView.xaml
    /// </summary>
    public partial class MultiMappingView : UserControl
    {
        // ViewModel instance with private setter to allow reassignment within the class
        public MultiMappingViewModel ViewModel { get; private set; }
        
        // Add these fields to store the references
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();

        public MultiMappingView()
        {
            InitializeComponent();
            
            // Create the ViewModel
            ViewModel = new MultiMappingViewModel();
            
            // Set DataContext
            this.DataContext = ViewModel;
            
            // Connect the activity log
            MultiModeActivityLog.ItemsSource = ViewModel.ActivityLog;

            // Subscribe to ViewModel events for file dialogs
            ViewModel.RequestSaveMappingsFilePath += ViewModel_RequestSaveMappingsFilePath;
            ViewModel.RequestLoadMappingsFilePath += ViewModel_RequestLoadMappingsFilePath;
        }

        // Add the new Initialize method
        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            this.midiOutput = output;
            this.mappingManager = mappingManager;
            
            // Update ViewModel with dependencies
            ViewModel = new MultiMappingViewModel();
            ViewModel.Initialize(output, mappingManager);
            
            // Set DataContext
            this.DataContext = ViewModel;
            MultiModeActivityLog.ItemsSource = ViewModel.ActivityLog;
            
            // Register for mapping events
            mappingManager.MappingsChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Update UI based on mapping changes if needed
                    // This would depend on how the MultiMappingView displays mappings
                });
            };
            
            // Register mapping event handler for logging
            mappingManager.RegisterMappingEventHandler(LogMidiEvent);

            // Subscribe to ViewModel events for file dialogs
            ViewModel.RequestSaveMappingsFilePath += ViewModel_RequestSaveMappingsFilePath;
            ViewModel.RequestLoadMappingsFilePath += ViewModel_RequestLoadMappingsFilePath;
        }

        // Method to handle controller input events
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            // Only let the ViewModel handle the input, not both MappingManager and ViewModel
            ViewModel.HandleControllerInput(e);
        }
        
        // Method to update the MIDI output reference (keep for backward compatibility)
        [Obsolete("Use Initialize(MidiOutput, MappingManager) instead")]
        public void SetMidiOutput(MidiOutput midiOutput)
        {
            this.midiOutput = midiOutput;
            
            // Create a new mapping manager for backward compatibility
            this.mappingManager = new MappingManager(midiOutput);
            
            // Update ViewModel
            ViewModel = new MultiMappingViewModel(midiOutput);
            this.DataContext = ViewModel;
            MultiModeActivityLog.ItemsSource = ViewModel.ActivityLog;
        }
        
        // Add logging method
        private void LogMidiEvent(string message)
        {
            // Add to in-memory log
            midiLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");

            // Update UI if available
            var midiActivityLog = this.FindName("MultiModeActivityLog") as ListBox;
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

        // Handle save file dialog request
        private void ViewModel_RequestSaveMappingsFilePath(object sender, EventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Save Multi-Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ViewModel.SaveMappingsToFile(dialog.FileName);
                    MessageBox.Show("Mappings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Handle load file dialog request
        private void ViewModel_RequestLoadMappingsFilePath(object sender, EventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Load Multi-Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ViewModel.LoadMappingsFromFile(dialog.FileName);
                    MessageBox.Show("Mappings loaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Error handler for command execution
        private void Command_Error(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
