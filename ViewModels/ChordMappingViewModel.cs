using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NAudio.Midi;
using XB2Midi.Commands;

namespace XB2Midi.ViewModels
{
    public class ChordMappingViewModel : INotifyPropertyChanged
    {
        private string _selectedChord;
        private string _midiOutput;
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

        public ObservableCollection<string> ActivityLog { get; } = new ObservableCollection<string>();

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

        public ObservableCollection<string> MidiDevices { get; } = new ObservableCollection<string>();

        public ICommand MapChordCommand { get; private set; }
        public ICommand ClearMappingCommand { get; private set; }

        public ChordMappingViewModel()
        {
            // Initialize properties
            AvailableChords = new ObservableCollection<string>
            {
                "C Major",
                "D Minor",
                "E Minor",
                "F Major",
                "G Major",
                "A Minor",
                "B Diminished"
            };

            SelectedChord = AvailableChords.Count > 0 ? AvailableChords[0] : null;
            MidiOutput = "MIDI Device 1";

            // Initialize commands
            MapChordCommand = new RelayCommand(ExecuteMapChord, CanExecuteMapChord);
            ClearMappingCommand = new RelayCommand(ExecuteClearMapping);

            // Load MIDI devices
            LoadMidiDevices();
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

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}