using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using XB2Midi.Models;

namespace XB2Midi.ViewModels
{
    public class MultiMappingViewModel : INotifyPropertyChanged
    {
        // Event for property changes
        public event PropertyChangedEventHandler? PropertyChanged;

        // MIDI output instance reference
        private MidiOutput? _midiOutput;
        
        // Activity log
        private ObservableCollection<string> _activityLog = new ObservableCollection<string>();
        public ObservableCollection<string> ActivityLog
        {
            get => _activityLog;
            set
            {
                _activityLog = value;
                OnPropertyChanged();
            }
        }

        // Constructor
        public MultiMappingViewModel(MidiOutput? midiOutput = null)
        {
            _midiOutput = midiOutput;
            LogActivity("Multi Mode initialized");
        }

        // Method to log activity
        public void LogActivity(string message)
        {
            string logEntry = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
            
            // Add to the ObservableCollection on the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ActivityLog.Insert(0, logEntry);
                
                // Keep log size manageable
                while (ActivityLog.Count > 100)
                {
                    ActivityLog.RemoveAt(ActivityLog.Count - 1);
                }
            });
            
            // Also log to debug output
            System.Diagnostics.Debug.WriteLine($"Multi Mode: {message}");
        }

        // Helper for property change notifications
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // Method to handle controller input in Multi mode
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            // Simple logging for now
            LogActivity($"Controller input: {e.InputType} - {e.InputName} = {e.Value}");
            
            // Future implementation will process controller input for multiple simultaneous mappings
        }
        
        // Method to reset the view
        public void Reset()
        {
            // Will be implemented when actual settings are added
            LogActivity("Multi Mode settings reset");
        }
    }
}
