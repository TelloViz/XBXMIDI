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
    /// Interaction logic for ArpeggioMappingView.xaml
    /// </summary>
    public partial class ArpeggioMappingView : UserControl
    {
        // ViewModel instance - Add private setter to allow reassignment within the class
        public ArpeggioMappingViewModel ViewModel { get; private set; }
        
        // Add these fields to store the references
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();

        public ArpeggioMappingView()
        {
            InitializeComponent();
            
            // Create the ViewModel
            ViewModel = new ArpeggioMappingViewModel();
            
            // Set DataContext
            this.DataContext = ViewModel;
            
            // Connect the activity log
            ArpeggioActivityLog.ItemsSource = ViewModel.ActivityLog;
        }

        // Add the new Initialize method
        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            this.midiOutput = output;
            this.mappingManager = mappingManager;
            
            // Update ViewModel with MIDI output
            ViewModel = new ArpeggioMappingViewModel(output);
            this.DataContext = ViewModel;
            ArpeggioActivityLog.ItemsSource = ViewModel.ActivityLog;
            
            // Register for mapping events
            mappingManager.MappingsChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Update UI based on mapping changes if needed
                    // This would depend on how the ArpeggioMappingView displays mappings
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
            
            // Then let the ViewModel handle any arpeggio-specific logic
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
            ViewModel = new ArpeggioMappingViewModel(midiOutput);
            this.DataContext = ViewModel;
            ArpeggioActivityLog.ItemsSource = ViewModel.ActivityLog;
        }
        
        // Add logging method
        private void LogMidiEvent(string message)
        {
            // Add to in-memory log
            midiLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");

            // Update UI if available
            var midiActivityLog = this.FindName("ArpeggioActivityLog") as ListBox;
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
