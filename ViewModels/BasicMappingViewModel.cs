using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using XB2Midi.Models;
using XB2Midi.Commands;
using NAudio.Midi; // Add this import
using XB2Midi.Services; // Add this for IMidiService and MidiService

namespace XB2Midi.ViewModels
{
    public class BasicMappingViewModel : INotifyPropertyChanged
    {
        private MappingManager mappingManager;
        private MidiOutput midiOutput;
        private IMidiService _midiService;
        public IMidiService MidiService 
        {
            get => _midiService;
            private set 
            {
                _midiService = value;
                OnPropertyChanged();
            }
        }

        // Observable collections for UI binding
        public ObservableCollection<string> ControllerInputs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MidiDevices { get; } = new ObservableCollection<string>();
        public ObservableCollection<MidiMapping> Mappings { get; } = new ObservableCollection<MidiMapping>();
        public ObservableCollection<string> ActivityLog { get; } = new ObservableCollection<string>();

        // Selected values
        private string selectedControllerInput;
        public string SelectedControllerInput
        {
            get => selectedControllerInput;
            set
            {
                selectedControllerInput = value;
                OnPropertyChanged();
            }
        }

        private string selectedMidiType = "Note";
        public string SelectedMidiType
        {
            get => selectedMidiType;
            set
            {
                selectedMidiType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMidiValueEnabled));
            }
        }

        private string selectedDevice;
        public string SelectedDevice
        {
            get => selectedDevice;
            set
            {
                selectedDevice = value;
                OnPropertyChanged();
            }
        }

        private string midiChannel = "1";
        public string MidiChannel
        {
            get => midiChannel;
            set
            {
                midiChannel = value;
                OnPropertyChanged();
            }
        }

        private string midiValue;
        public string MidiValue
        {
            get => midiValue;
            set
            {
                midiValue = value;
                OnPropertyChanged();
            }
        }

        private MidiMapping selectedMapping;
        public MidiMapping SelectedMapping
        {
            get => selectedMapping;
            set
            {
                selectedMapping = value;
                OnPropertyChanged();
                DeleteMappingCommand.RaiseCanExecuteChanged();
            }
        }

        // Derived properties
        public bool IsMidiValueEnabled => SelectedMidiType != "Pitch Bend";

        // Constructor injection
        public BasicMappingViewModel(IMidiService midiService = null)
        {
            _midiService = midiService;
            InitializeCommands();
            PopulateControllerInputs();
        }

        public void Initialize(MidiOutput output, MappingManager manager)
        {
            // Create MidiService if not injected
            if (_midiService == null && output != null)
            {
                MidiService = new MidiService(output);
            }
            
            mappingManager = manager;
            
            // Rest remains the same
            mappingManager.MappingsChanged += (s, e) => RefreshMappings();
            mappingManager.RegisterMappingEventHandler(LogMidiEvent);
            
            RefreshMappings();
            RefreshMidiDevices();
        }

        // Commands
        public RelayCommand AddMappingCommand { get; private set; }
        public RelayCommand<MidiMapping> DeleteMappingCommand { get; private set; }
        public RelayCommand SaveMappingsCommand { get; private set; }
        public RelayCommand LoadMappingsCommand { get; private set; }
        public RelayCommand RefreshDevicesCommand { get; private set; }
        
        private void InitializeCommands()
        {
            // Convert parameterless methods to accept an object parameter
            AddMappingCommand = new RelayCommand(_ => AddMapping(), _ => CanAddMapping());
            
            // For generic commands that already take parameters, adapt the signature
            DeleteMappingCommand = new RelayCommand<MidiMapping>(
                mapping => DeleteMapping(mapping), 
                mapping => CanDeleteMapping(mapping));
            
            // Convert remaining parameterless methods
            SaveMappingsCommand = new RelayCommand(_ => SaveMappings(), _ => mappingManager != null);
            LoadMappingsCommand = new RelayCommand(_ => LoadMappings(), _ => mappingManager != null);
            RefreshDevicesCommand = new RelayCommand(_ => RefreshMidiDevices());
        }
        
        // Handler methods
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            mappingManager?.HandleControllerInput(e);
        }
        
        // Implementation methods
        private void PopulateControllerInputs()
        {
            ControllerInputs.Clear();
            var inputs = new List<string> {
                "A", "B", "X", "Y",
                "LeftBumper", "RightBumper",
                "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
                "LeftTrigger", "RightTrigger",
                "LeftThumbstickX", "LeftThumbstickY",
                "RightThumbstickX", "RightThumbstickY"
            };
            
            foreach (var input in inputs)
            {
                ControllerInputs.Add(input);
            }
            
            if (ControllerInputs.Count > 0)
                SelectedControllerInput = ControllerInputs[0];
        }
        
        private void RefreshMidiDevices()
        {
            MidiDevices.Clear();
            
            int deviceCount = _midiService.GetNumberOfMidiDevices();
            for (int i = 0; i < deviceCount; i++)
            {
                MidiDevices.Add($"{i}: {_midiService.GetMidiDeviceName(i)}");
            }
            
            if (MidiDevices.Count > 0)
                SelectedDevice = MidiDevices[0];
        }
        
        private void RefreshMappings()
        {
            Mappings.Clear();
            var currentMappings = mappingManager?.GetCurrentMappings();
            if (currentMappings != null)
            {
                foreach (var mapping in currentMappings)
                {
                    Mappings.Add(mapping);
                }
            }
        }
        
        private void LogMidiEvent(string message)
        {
            ActivityLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");
            
            // Keep log size manageable
            while (ActivityLog.Count > 100)
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
        
        // Command implementations
        private bool CanAddMapping()
        {
            return mappingManager != null && 
                   !string.IsNullOrEmpty(SelectedControllerInput) && 
                   !string.IsNullOrEmpty(SelectedMidiType) && 
                   !string.IsNullOrEmpty(SelectedDevice);
        }
        
        private void AddMapping()
        {
            try
            {
                if (!byte.TryParse(MidiChannel, out byte channel) || channel < 1 || channel > 16)
                {
                    throw new ArgumentException("Please enter a valid MIDI channel (1-16).");
                }

                // Adjust channel to be 0-based for internal handling
                channel--;

                MidiMessageType messageType = SelectedMidiType switch
                {
                    "Note" => MidiMessageType.Note,
                    "Control Change" => MidiMessageType.ControlChange,
                    "Pitch Bend" => MidiMessageType.PitchBend,
                    _ => MidiMessageType.ControlChange
                };

                string deviceString = SelectedDevice;
                int deviceIndex = int.Parse(deviceString.Split(':')[0]);
                
                var mapping = new MidiMapping
                {
                    ControllerInput = SelectedControllerInput.Replace(" Button", "").Replace(" ", ""),
                    MessageType = messageType,
                    Channel = channel,
                    MinValue = 0,
                    MaxValue = messageType == MidiMessageType.PitchBend ? 16383 : 127,
                    MidiDeviceIndex = deviceIndex,
                    MidiDeviceName = deviceString
                };

                if (messageType != MidiMessageType.PitchBend)
                {
                    if (!byte.TryParse(MidiValue, out byte value) || value > 127)
                    {
                        throw new ArgumentException("Please enter a valid value (0-127).");
                    }

                    if (messageType == MidiMessageType.Note)
                    {
                        mapping.NoteNumber = value;
                    }
                    else
                    {
                        mapping.ControllerNumber = value;
                    }
                }

                mappingManager?.AddMapping(mapping);
                LogMidiEvent($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType} on device {mapping.MidiDeviceName}");
            }
            catch (Exception ex)
            {
                // Will need to handle this in the view
                throw new ApplicationException($"Error adding mapping: {ex.Message}", ex);
            }
        }
        
        private bool CanDeleteMapping(MidiMapping mapping)
        {
            return mapping != null && mappingManager != null;
        }
        
        private void DeleteMapping(MidiMapping mapping)
        {
            if (mapping != null && mappingManager != null)
            {
                mappingManager.RemoveMapping(mapping);
                LogMidiEvent($"Removed mapping for {mapping.ControllerInput}");
            }
        }
        
        private void SaveMappings()
        {
            // Will need to prompt for file path in the view
            // This just prepares the functionality
            if (mappingManager != null)
            {
                RequestSaveMappingsFilePath?.Invoke(this, EventArgs.Empty);
            }
        }
        
        public void SaveMappingsToFile(string filePath)
        {
            try
            {
                mappingManager?.SaveMappings(filePath);
                LogMidiEvent($"Mappings saved to {filePath}");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error saving mappings: {ex.Message}", ex);
            }
        }
        
        private void LoadMappings()
        {
            // Will need to prompt for file path in the view
            RequestLoadMappingsFilePath?.Invoke(this, EventArgs.Empty);
        }
        
        public void LoadMappingsFromFile(string filePath)
        {
            try
            {
                mappingManager?.LoadMappings(filePath);
                LogMidiEvent($"Mappings loaded from {filePath}");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error loading mappings: {ex.Message}", ex);
            }
        }
        
        // Events for view interaction
        public event EventHandler RequestSaveMappingsFilePath;
        public event EventHandler RequestLoadMappingsFilePath;
        
        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
