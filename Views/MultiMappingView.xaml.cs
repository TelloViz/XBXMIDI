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
        }

        // Add the new Initialize method
        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            this.midiOutput = output;
            this.mappingManager = mappingManager;
            
            // Update ViewModel with MIDI output
            ViewModel = new MultiMappingViewModel(output);
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
        }

        // Method to handle controller input events
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            // Pass to the mapping manager first
            if (mappingManager != null)
            {
                mappingManager.HandleControllerInput(e);
            }
            
            // Then let the ViewModel handle any multi-specific logic
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
    }
}
