using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using XB2Midi.Models;
using XB2Midi.ViewModels;
using XB2Midi.Services;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for ArpeggioMappingView.xaml
    /// Note: This is a placeholder implementation. Specific arpeggio functionality will be added later.
    /// </summary>
    public partial class ArpeggioMappingView : UserControl
    {
        // ViewModel instance
        public ArpeggioMappingViewModel? ViewModel { get; private set; }
        
        // Private fields
        private MidiOutput? midiOutput;
        private ArpeggioMappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();
        private readonly IDialogService _dialogService;

        public ArpeggioMappingView()
        {
            InitializeComponent();
            
            // Create dialog service
            _dialogService = new DialogService();
            
            // Show placeholder message
            MessageTextBlock.Text = "Arpeggio Mode is not yet implemented. Coming soon!";
        }

        /// <summary>
        /// Initialize the view with dependencies
        /// </summary>
        /// <param name="output">MIDI output device</param>
        /// <param name="manager">Arpeggio mapping manager</param>
        public void Initialize(MidiOutput output, ArpeggioMappingManager manager)
        {
            midiOutput = output;
            mappingManager = manager;
            
            // Register for events
            mappingManager.RegisterMappingEventHandler(LogMidiEvent);
        }
        
        // Log MIDI events
        private void LogMidiEvent(string message)
        {
            // Add to in-memory log
            midiLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");
            
            // Keep log size reasonable
            while (midiLog.Count > 100)
                midiLog.RemoveAt(midiLog.Count - 1);
                
            // Log to debug output
            System.Diagnostics.Debug.WriteLine($"Arpeggio MIDI: {message}");
        }
    }
}
