using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Diagnostics;
using XB2Midi.Models;
using XB2Midi.ViewModels;
using XB2Midi.Services;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for MultiMappingView.xaml
    /// </summary>
    public partial class MultiMappingView : UserControl
    {
        // ViewModel instance with private setter to allow reassignment within the class
        public MultiMappingViewModel ViewModel { get; private set; }
        
        private MidiOutput? midiOutput;
        private MultiMappingManager? mappingManager; // Change to MultiMappingManager
        private ObservableCollection<string> midiLog = new();
        private readonly IDialogService _dialogService;

        public MultiMappingView() {
            InitializeComponent();
            
            // Create dialog service
            _dialogService = new DialogService();
            
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

        // Update Initialize method to use MultiMappingManager
        public void Initialize(MidiOutput output, MultiMappingManager mappingManager) {
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
                });
            };
            
            // Register mapping event handler for logging
            mappingManager.RegisterMappingEventHandler(LogMidiEvent);

            // Subscribe to ViewModel events for file dialogs
            ViewModel.RequestSaveMappingsFilePath += ViewModel_RequestSaveMappingsFilePath;
            ViewModel.RequestLoadMappingsFilePath += ViewModel_RequestLoadMappingsFilePath;
        }

        // Method to handle controller input events
        public void HandleControllerInput(ControllerInputEventArgs e) {
            // Forward to the specialized mapping manager
            mappingManager?.HandleControllerInput(e);
        }
        
        // Method to update the MIDI output reference (keep for backward compatibility)
        [Obsolete("Use Initialize(MidiOutput, MultiMappingManager) instead")]
        public void SetMidiOutput(MidiOutput midiOutput) {
            this.midiOutput = midiOutput;
            
            // Create a new mapping manager for backward compatibility
            this.mappingManager = new MultiMappingManager(midiOutput);
            
            // Update ViewModel
            ViewModel = new MultiMappingViewModel(midiOutput);
            this.DataContext = ViewModel;
            MultiModeActivityLog.ItemsSource = ViewModel.ActivityLog;
        }
        
        private void LogMidiEvent(string message) {
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
        private void ViewModel_RequestSaveMappingsFilePath(object sender, EventArgs e) {
            // Get the parent window
            Window parentWindow = Window.GetWindow(this);
            
            // Use dialog service to show save dialog
            string filePath = _dialogService.ShowSaveFileDialog(
                parentWindow,
                "Save Multi Mode Mappings", 
                "Multi Mode Mappings (*.multi.json)|*.multi.json|JSON files (*.json)|*.json|All files (*.*)|*.*", 
                ".multi.json");
                
            if (filePath != null)
            {
                try
                {
                    ViewModel.SaveMappingsToFile(filePath);
                    _dialogService.ShowMessage(parentWindow, "Multi mode mappings saved successfully!", "Success");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError(parentWindow, ex.Message);
                }
            }
        }

        // Handle load file dialog request
        private void ViewModel_RequestLoadMappingsFilePath(object sender, EventArgs e) {
            // Get the parent window
            Window parentWindow = Window.GetWindow(this);
            
            // Use dialog service to show open dialog
            string filePath = _dialogService.ShowOpenFileDialog(
                parentWindow,
                "Load Multi Mode Mappings", 
                "Multi Mode Mappings (*.multi.json)|*.multi.json|JSON files (*.json)|*.json|All files (*.*)|*.*", 
                ".multi.json");
                
            if (filePath != null)
            {
                try
                {
                    ViewModel.LoadMappingsFromFile(filePath);
                    _dialogService.ShowMessage(parentWindow, "Multi mode mappings loaded successfully!", "Success");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError(parentWindow, ex.Message);
                }
            }
        }

        // Error handler for command execution
        private void Command_Error(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
            if (e.Parameter is Exception ex)
            {
                Window parentWindow = Window.GetWindow(this);
                _dialogService.ShowError(parentWindow, ex.Message);
            }
        }
    }
}
