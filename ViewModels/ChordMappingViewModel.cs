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

        #region Chord Toggles
        private bool _rootToggleChecked = true;
        public bool RootToggleChecked
        {
            get => _rootToggleChecked;
            set
            {
                if (_rootToggleChecked != value)
                {
                    _rootToggleChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _majThirdToggleChecked;
        public bool MajThirdToggleChecked
        {
            get => _majThirdToggleChecked;
            set
            {
                if (_majThirdToggleChecked != value)
                {
                    _majThirdToggleChecked = value;
                    OnPropertyChanged();
                    if (value && MinThirdToggleChecked)
                    {
                        MinThirdToggleChecked = false;
                    }
                }
            }
        }

        private bool _minThirdToggleChecked;
        public bool MinThirdToggleChecked
        {
            get => _minThirdToggleChecked;
            set
            {
                if (_minThirdToggleChecked != value)
                {
                    _minThirdToggleChecked = value;
                    OnPropertyChanged();
                    if (value && MajThirdToggleChecked)
                    {
                        MajThirdToggleChecked = false;
                    }
                }
            }
        }

        private bool _fifthToggleChecked;
        public bool FifthToggleChecked
        {
            get => _fifthToggleChecked;
            set
            {
                if (_fifthToggleChecked != value)
                {
                    _fifthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && FlatFifthToggleChecked)
                        FlatFifthToggleChecked = false;
                }
            }
        }

        private bool _flatFifthToggleChecked;
        public bool FlatFifthToggleChecked
        {
            get => _flatFifthToggleChecked;
            set
            {
                if (_flatFifthToggleChecked != value)
                {
                    _flatFifthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && FifthToggleChecked)
                        FifthToggleChecked = false;
                }
            }
        }

        private bool _sixthToggleChecked;
        public bool SixthToggleChecked
        {
            get => _sixthToggleChecked;
            set
            {
                if (_sixthToggleChecked != value)
                {
                    _sixthToggleChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _domSeventhToggleChecked;
        public bool DomSeventhToggleChecked
        {
            get => _domSeventhToggleChecked;
            set
            {
                if (_domSeventhToggleChecked != value)
                {
                    _domSeventhToggleChecked = value;
                    OnPropertyChanged();
                    if (value && MajSeventhToggleChecked)
                        MajSeventhToggleChecked = false;
                }
            }
        }

        private bool _majSeventhToggleChecked;
        public bool MajSeventhToggleChecked
        {
            get => _majSeventhToggleChecked;
            set
            {
                if (_majSeventhToggleChecked != value)
                {
                    _majSeventhToggleChecked = value;
                    OnPropertyChanged();
                    if (value && DomSeventhToggleChecked)
                        DomSeventhToggleChecked = false;
                }
            }
        }

        private bool _ninthToggleChecked;
        public bool NinthToggleChecked
        {
            get => _ninthToggleChecked;
            set
            {
                if (_ninthToggleChecked != value)
                {
                    _ninthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && FlatNinthToggleChecked)
                        FlatNinthToggleChecked = false;
                }
            }
        }

        private bool _flatNinthToggleChecked;
        public bool FlatNinthToggleChecked
        {
            get => _flatNinthToggleChecked;
            set
            {
                if (_flatNinthToggleChecked != value)
                {
                    _flatNinthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && NinthToggleChecked)
                        NinthToggleChecked = false;
                }
            }
        }
        #endregion // End Chord Toggles


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
        public ICommand ClearChordCommand { get; private set; } // Clears the sample chord

        public ChordMappingViewModel()
        {
            // Initialize properties
            AvailableChords = new ObservableCollection<string> // TODO Not sure what this is for
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
            ClearMappingCommand = new RelayCommand(ExecuteClearMapping); // Clear Mapping Command
            ClearChordCommand = new RelayCommand(_ => ClearChordToggles()); // Clear Sample Chord Command

            LoadMidiDevices(); // Load MIDI devices on initialization

        }


        private void ClearChordToggles()
        {
            RootToggleChecked = true;
            MajThirdToggleChecked = false;
            MinThirdToggleChecked = false;
            FifthToggleChecked = false;
            FlatFifthToggleChecked = false;
            SixthToggleChecked = false;
            DomSeventhToggleChecked = false;
            MajSeventhToggleChecked = false;
            NinthToggleChecked = false;
            FlatNinthToggleChecked = false;

            ActivityLog.Insert(0, $"{DateTime.Now:HHmm:ss.fff} - Chord cleared");
            while (ActivityLog.Count > 100)
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
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