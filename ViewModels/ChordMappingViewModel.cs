using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Collections.Generic; // For List<>
using NAudio.Midi;
using XB2Midi.Commands;
using XB2Midi.Models;
using System.Diagnostics;  
using XB2Midi.Services;  // Add this for IMidiService and IDialogService
using XB2Midi.Utilities; // Add this for MusicTheory

namespace XB2Midi.ViewModels
{
    public class ChordMappingViewModel : INotifyPropertyChanged
    {
        private string _selectedChord;

        private string _midiOutput;
        public string MidiOutput
        {
            get => _midiOutput;
            set
            {
                if (_midiOutput != value)
                {
                    _midiOutput = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _availableChords;
        private int _selectedMidiDeviceIndex;

        public string SelectedChord
        {
            get => _selectedChord;
            set
            {
                if (_selectedChord != value)
                {
                    _selectedChord = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> AvailableChords
        {
            get => _availableChords;
            set
            {
                if (_availableChords != value)
                {
                    _availableChords = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> ActivityLog { get; private set; }

        public int SelectedMidiDeviceIndex
        {
            get => _selectedMidiDeviceIndex;
            set
            {
                if (_selectedMidiDeviceIndex != value)
                {
                    _selectedMidiDeviceIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> MidiDevices { get; private set; }

        public RelayCommand MapChordCommand { get; private set; }
        public RelayCommand ClearMappingCommand { get; private set; }

        private readonly IMidiService _midiService;
        private readonly IDialogService _dialogService;

        public ChordMappingViewModel(IMidiService midiService, IDialogService dialogService)
        {
            _midiService = midiService;
            _dialogService = dialogService;
            ActivityLog = new ObservableCollection<string>();
            AvailableChords = new ObservableCollection<string>();
            MidiDevices = new ObservableCollection<string>();
            
            // Initialize commands
            // (We'll add these later)
        }

        private void InitializeCommands()
        {
        }

        public void LoadMidiDevices()
        {
            MidiDevices.Clear();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                MidiDevices.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
            }
            if (MidiDevices.Count > 0)
            {
                SelectedMidiDeviceIndex = 0;
            }
        }

        private bool CanExecuteMapChord(object parameter) => !string.IsNullOrEmpty(SelectedChord);

        private void ExecuteMapChord(object parameter)
        {
            // TODO: Implement actual chord mapping functionality
            Console.WriteLine($"Mapping chord: {SelectedChord} to {MidiOutput}");
        }

        private void ExecuteClearMapping(object parameter)
        {
            // TODO: Implement clearing of chord mapping
            Console.WriteLine("Clearing chord mappings");
        }

        /// <summary>
        /// Logs MIDI events to the activity log and outputs to Debug.
        /// </summary>
        /// <param name="message">The message to log</param>
        public void LogMidiEvent(string message)
        {
            // Add timestamp
            string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
            
            // Add to activity log
            ActivityLog.Insert(0, timestampedMessage);
            
            // Limit collection size
            while (ActivityLog.Count > 100)
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
            
            // Also log to Debug
            Debug.WriteLine($"MIDI: {message}");
        }

        /// <summary>
        /// Plays a custom chord using the MIDI service.
        /// </summary>
        /// <param name="chordNotes">List of notes in the chord</param>
        /// <param name="rootNote">Root note of the chord</param>
        /// <param name="inversionLevel">Inversion level to apply</param>
        /// <param name="deviceIndex">MIDI device index</param>
        public void PlayChord(List<byte> chordNotes, byte rootNote, int inversionLevel, int deviceIndex)
        {
            // Skip if no notes are selected
            if (chordNotes.Count == 0)
                return;
            
            // Apply inversion if specified
            if (inversionLevel > 0)
            {
                chordNotes = MusicTheory.ApplyInversion(chordNotes, inversionLevel);
            }
            
            // Play the chord
            byte velocity = 100;
            byte channel = 0;
            
            _midiService.PlayChord(chordNotes, deviceIndex, channel, velocity);
            
            // Generate chord name for logging
            string chordName = MusicTheory.DetermineChordName(chordNotes, rootNote);
            
            // Add inversion information to the log message
            string inversionText = inversionLevel == 0 ? "" : $" ({MusicTheory.GetInversionName(inversionLevel)})";
            
            LogMidiEvent($"Custom chord played: {MusicTheory.GetNoteName(rootNote)} {chordName}{inversionText}");
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

}