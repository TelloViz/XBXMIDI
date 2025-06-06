using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NAudio.Midi;
using XB2Midi.Commands;
using XB2Midi.Models;

namespace XB2Midi.ViewModels
{
    public class MappingGroup : INotifyPropertyChanged
    {
        public string ControllerInput { get; set; }
        public ObservableCollection<MultiMapping> Mappings { get; } = new ObservableCollection<MultiMapping>();
        public int MappingCount => Mappings.Count;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MultiMappingViewModel : INotifyPropertyChanged
    {
        // Event for property changes
        public event PropertyChangedEventHandler? PropertyChanged;

        // MIDI and mapping dependencies
        private MidiOutput? _midiOutput;
        private MappingManager? _mappingManager;
        
        // Observable collections for UI binding
        public ObservableCollection<string> ControllerInputs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MidiDevices { get; } = new ObservableCollection<string>();
        public ObservableCollection<MultiMapping> Mappings { get; } = new ObservableCollection<MultiMapping>();
        public ObservableCollection<string> ActivityLog { get; } = new ObservableCollection<string>();

        // Add this property for grouped mappings display
        private ObservableCollection<MappingGroup> _groupedMappings = new ObservableCollection<MappingGroup>();
        public ObservableCollection<MappingGroup> GroupedMappings
        {
            get => _groupedMappings;
            private set
            {
                _groupedMappings = value;
                OnPropertyChanged();
            }
        }

        // Selected controller input
        private string _selectedControllerInput;
        public string SelectedControllerInput
        {
            get => _selectedControllerInput;
            set
            {
                _selectedControllerInput = value;
                OnPropertyChanged();
                RefreshMappingsForSelectedInput();
            }
        }

        // Selected MIDI type
        private string _selectedMidiType = "Note";
        public string SelectedMidiType
        {
            get => _selectedMidiType;
            set
            {
                _selectedMidiType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMidiValueEnabled));
            }
        }

        // Selected MIDI device
        private string _selectedDevice;
        public string SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
            }
        }

        // MIDI channel
        private string _midiChannel = "1";
        public string MidiChannel
        {
            get => _midiChannel;
            set
            {
                _midiChannel = value;
                OnPropertyChanged();
            }
        }

        // MIDI value (for note number or controller number)
        private string _midiValue;
        public string MidiValue
        {
            get => _midiValue;
            set
            {
                _midiValue = value;
                OnPropertyChanged();
            }
        }

        // Selected mapping for deletion
        private MultiMapping _selectedMapping;
        public MultiMapping SelectedMapping
        {
            get => _selectedMapping;
            set
            {
                _selectedMapping = value;
                OnPropertyChanged();
                DeleteMappingCommand.RaiseCanExecuteChanged();
            }
        }

        // Derived property to enable/disable MIDI value based on type
        public bool IsMidiValueEnabled => SelectedMidiType != "Pitch Bend";

        // Commands
        public RelayCommand AddMappingCommand { get; private set; }
        public RelayCommand<MultiMapping> DeleteMappingCommand { get; private set; }
        public RelayCommand SaveMappingsCommand { get; private set; }
        public RelayCommand LoadMappingsCommand { get; private set; }
        public RelayCommand RefreshDevicesCommand { get; private set; }

        // Constructor
        public MultiMappingViewModel(MidiOutput? midiOutput = null)
        {
            _midiOutput = midiOutput;
            InitializeCommands();
            PopulateControllerInputs();
            
            if (midiOutput != null)
            {
                RefreshMidiDevices();
            }
            
            LogActivity("Multi Mode initialized");
        }

        // Initialize with dependencies
        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            _midiOutput = output;
            _mappingManager = mappingManager;
            
            // Refresh devices and mappings
            RefreshMidiDevices();
            RefreshMappings();
            
            // Register for mapping events
            if (_mappingManager != null)
            {
                _mappingManager.MappingsChanged += (s, e) => RefreshMappings();
                _mappingManager.RegisterMappingEventHandler(LogActivity);
            }
        }

        // Initialize commands
        private void InitializeCommands()
        {
            AddMappingCommand = new RelayCommand(_ => AddMapping(), _ => CanAddMapping());
            DeleteMappingCommand = new RelayCommand<MultiMapping>(
                mapping => DeleteMapping(mapping),
                mapping => mapping != null);
            SaveMappingsCommand = new RelayCommand(_ => SaveMappings(), _ => _mappingManager != null);
            LoadMappingsCommand = new RelayCommand(_ => LoadMappings(), _ => _mappingManager != null);
            RefreshDevicesCommand = new RelayCommand(_ => RefreshMidiDevices());
        }

        // Populate controller inputs
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

        // Refresh MIDI devices
        private void RefreshMidiDevices()
        {
            MidiDevices.Clear();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                MidiDevices.Add($"{i:00}: {MidiOut.DeviceInfo(i).ProductName}");
            }
            
            if (MidiDevices.Count > 0)
                SelectedDevice = MidiDevices[0];
        }

        // Refresh all mappings
        private void RefreshMappings()
        {
            Mappings.Clear();
            GroupedMappings.Clear();
            
            if (_mappingManager != null)
            {
                var allMappings = _mappingManager.GetCurrentMappings()
                    .Where(m => m.Mode == MappingMode.Multi)
                    .ToList();
                
                // Group mappings by controller input
                var groupedMappings = allMappings
                    .GroupBy(m => m.ControllerInput)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedMappings)
                {
                    var mappingGroup = new MappingGroup { ControllerInput = group.Key };
                    foreach (var mapping in group.OrderBy(m => m.MessageType))
                    {
                        var multiMapping = new MultiMapping(mapping);
                        mappingGroup.Mappings.Add(multiMapping);
                        Mappings.Add(multiMapping);
                    }
                    GroupedMappings.Add(mappingGroup);
                }
            }
            
            RefreshMappingsForSelectedInput();
        }

        // Add this property to expose only mappings for the selected input
        private ObservableCollection<MultiMapping> _selectedInputMappings = new ObservableCollection<MultiMapping>();
        public ObservableCollection<MultiMapping> SelectedInputMappings
        {
            get => _selectedInputMappings;
            private set
            {
                _selectedInputMappings = value;
                OnPropertyChanged();
            }
        }

        // Refresh mappings for selected input
        private void RefreshMappingsForSelectedInput()
        {
            if (string.IsNullOrEmpty(SelectedControllerInput))
            {
                SelectedInputMappings = new ObservableCollection<MultiMapping>();
                return;
            }

            var filtered = Mappings
                .Where(m => m.ControllerInput == SelectedControllerInput)
                .ToList();

            SelectedInputMappings = new ObservableCollection<MultiMapping>(filtered);
        }

        // Method to handle controller input
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            if (_midiOutput == null || _mappingManager == null) return;

            // Get all unique mappings for this input, using ToHashSet to ensure uniqueness
            var inputMappings = _mappingManager.GetCurrentMappings()
                .Where(m => m.Mode == MappingMode.Multi && 
                           m.ControllerInput == e.InputName)
                .ToHashSet(new MidiMappingEqualityComparer());

            if (!inputMappings.Any()) return;

            foreach (var mapping in inputMappings.OrderBy(m => m.MessageType))
            {
                switch (mapping.MessageType)
                {
                    case MidiMessageType.Note:
                        if (Convert.ToBoolean(e.Value))
                        {
                            _midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, (byte)mapping.MaxValue);
                            LogActivity($"Multi Mode: {mapping.ControllerInput} -> Note On {mapping.NoteNumber} on device {mapping.MidiDeviceName}");
                        }
                        else
                        {
                            _midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber);
                            LogActivity($"Multi Mode: {mapping.ControllerInput} -> Note Off {mapping.NoteNumber} on device {mapping.MidiDeviceName}");
                        }
                        break;

                    case MidiMessageType.ControlChange:
                        byte ccValue = Convert.ToBoolean(e.Value) ? (byte)mapping.MaxValue : (byte)mapping.MinValue;
                        _midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, mapping.ControllerNumber, ccValue);
                        LogActivity($"Multi Mode: {mapping.ControllerInput} -> CC {mapping.ControllerNumber} value:{ccValue} on device {mapping.MidiDeviceName}");
                        break;

                    case MidiMessageType.PitchBend:
                        if (e.InputType == ControllerInputType.Thumbstick || e.InputType == ControllerInputType.Trigger)
                        {
                            double value = Convert.ToDouble(e.Value);
                            double normalizedValue = (value + 1.0) / 2.0;
                            int pitchBendValue = (int)(normalizedValue * 16383);
                            _midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchBendValue);
                            LogActivity($"Multi Mode: {mapping.ControllerInput} -> Pitch Bend value:{pitchBendValue} on device {mapping.MidiDeviceName}");
                        }
                        break;
                }
            }
        }

        // Log activity
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
        }

        // Can add mapping validation
        private bool CanAddMapping()
        {
            return _mappingManager != null && 
                   !string.IsNullOrEmpty(SelectedControllerInput) && 
                   !string.IsNullOrEmpty(SelectedMidiType) && 
                   !string.IsNullOrEmpty(SelectedDevice);
        }

        // Fix the AddMapping method
        private void AddMapping()
        {
            try
            {
                if (!byte.TryParse(MidiChannel, out byte channel) || channel < 1 || channel > 16)
                    throw new ArgumentException("Please enter a valid MIDI channel (1-16).");

                channel--;

                // Fix: Map the selected type string directly to MidiMessageType
                MidiMessageType messageType;
                switch (SelectedMidiType)
                {
                    case "Note":
                        messageType = MidiMessageType.Note;
                        break;
                    case "ControlChange":
                        messageType = MidiMessageType.ControlChange;
                        break;
                    case "PitchBend":
                        messageType = MidiMessageType.PitchBend;
                        break;
                    default:
                        throw new ArgumentException($"Invalid MIDI message type: {SelectedMidiType}");
                }

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
                    MidiDeviceName = deviceString,
                    Mode = MappingMode.Multi
                };

                if (messageType != MidiMessageType.PitchBend)
                {
                    if (!byte.TryParse(MidiValue, out byte value) || value > 127)
                        throw new ArgumentException("Please enter a valid value (0-127).");

                    if (messageType == MidiMessageType.Note)
                        mapping.NoteNumber = value;
                    else
                        mapping.ControllerNumber = value;
                }

                // Check using our consistent comparison method
                var existingMapping = _mappingManager?.GetCurrentMappings()
                    .FirstOrDefault(m => AreMappingsEqual(m, mapping));

                if (existingMapping != null)
                {
                    throw new ApplicationException("A similar mapping already exists for this input.");
                }

                _mappingManager?.AddMapping(mapping);
                LogActivity($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType} on device {mapping.MidiDeviceName}");

                RefreshMappings();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error adding mapping: {ex.Message}", ex);
            }
        }

        // Add this helper method for consistent mapping comparison
        private bool AreMappingsEqual(MidiMapping x, MidiMapping y)
        {
            if (x == null || y == null) return false;

            bool basicMatch = x.Mode == y.Mode &&
                            x.ControllerInput == y.ControllerInput &&
                            x.MessageType == y.MessageType &&
                            x.Channel == y.Channel &&
                            x.MidiDeviceIndex == y.MidiDeviceIndex;

            if (!basicMatch) return false;

            switch (x.MessageType)
            {
                case MidiMessageType.Note:
                    return x.NoteNumber == y.NoteNumber;
                case MidiMessageType.ControlChange:
                    return x.ControllerNumber == y.ControllerNumber;
                case MidiMessageType.PitchBend:
                    return true;
                default:
                    return false;
            }
        }

        // Delete a mapping
        private void DeleteMapping(MultiMapping mapping)
        {
            if (mapping != null && _mappingManager != null)
            {
                // Create mapping object for comparison
                var mappingToDelete = new MidiMapping
                {
                    ControllerInput = mapping.ControllerInput,
                    MessageType = mapping.MessageType,
                    Channel = mapping.Channel,
                    NoteNumber = mapping.NoteNumber,
                    ControllerNumber = mapping.ControllerNumber,
                    MidiDeviceIndex = mapping.DeviceIndex,
                    MidiDeviceName = mapping.DeviceName,
                    MinValue = 0,
                    MaxValue = mapping.MessageType == MidiMessageType.PitchBend ? 16383 : 127,
                    Mode = MappingMode.Multi
                };

                // Find and remove the exact mapping
                var currentMappings = _mappingManager.GetCurrentMappings().ToList();
                var matchingMapping = currentMappings.FirstOrDefault(m => AreMappingsEqual(m, mappingToDelete));

                if (matchingMapping != null)
                {
                    _mappingManager.RemoveMapping(matchingMapping);
                    LogActivity($"Removed mapping for {mapping.ControllerInput}");

                    // Clear both collections to force a fresh reload
                    Mappings.Clear();
                    SelectedInputMappings.Clear();

                    // Reload mappings from MappingManager
                    var currentMappingsReloaded = _mappingManager.GetCurrentMappings()
                        .Where(m => m.Mode == MappingMode.Multi)
                        .ToList();

                    foreach (var m in currentMappingsReloaded)
                    {
                        Mappings.Add(new MultiMapping(m));
                    }

                    // Refresh filtered mappings
                    RefreshMappingsForSelectedInput();
                }
            }
        }

        // Save mappings
        private void SaveMappings()
        {
            // Trigger the file dialog in the view
            RequestSaveMappingsFilePath?.Invoke(this, EventArgs.Empty);
        }

        // Load mappings
        private void LoadMappings()
        {
            // Trigger the file dialog in the view
            RequestLoadMappingsFilePath?.Invoke(this, EventArgs.Empty);
        }

        // Save mappings to file
        public void SaveMappingsToFile(string filePath)
        {
            try
            {
                _mappingManager?.SaveMappings(filePath);
                LogActivity($"Mappings saved to {filePath}");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error saving mappings: {ex.Message}", ex);
            }
        }

        // Load mappings from file
        public void LoadMappingsFromFile(string filePath)
        {
            try
            {
                _mappingManager?.LoadMappings(filePath);
                LogActivity($"Mappings loaded from {filePath}");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error loading mappings: {ex.Message}", ex);
            }
        }

        // Reset the view
        public void Reset()
        {
            // Clear any user-specific settings
            LogActivity("Multi Mode settings reset");
        }

        // Events for view interaction
        public event EventHandler RequestSaveMappingsFilePath;
        public event EventHandler RequestLoadMappingsFilePath;

        // INotifyPropertyChanged implementation
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Add this class inside MultiMappingViewModel
        private class MidiMappingEqualityComparer : IEqualityComparer<MidiMapping>
        {
            public bool Equals(MidiMapping x, MidiMapping y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;

                return x.MessageType == y.MessageType &&
                       x.Channel == y.Channel &&
                       x.MidiDeviceIndex == y.MidiDeviceIndex &&
                       ((x.MessageType == MidiMessageType.Note && x.NoteNumber == y.NoteNumber) ||
                        (x.MessageType == MidiMessageType.ControlChange && x.ControllerNumber == y.ControllerNumber) ||
                        x.MessageType == MidiMessageType.PitchBend);
            }

            public int GetHashCode(MidiMapping obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.MessageType);
                hashCode.Add(obj.Channel);
                hashCode.Add(obj.MidiDeviceIndex);
                
                if (obj.MessageType == MidiMessageType.Note)
                    hashCode.Add(obj.NoteNumber);
                else if (obj.MessageType == MidiMessageType.ControlChange)
                    hashCode.Add(obj.ControllerNumber);

                return hashCode.ToHashCode();
            }
        }
    }

    // Helper class for multi-mappings display
    public class MultiMapping
    {
        public string ControllerInput { get; set; }
        public MidiMessageType MessageType { get; set; }
        public byte Channel { get; set; }
        public byte NoteNumber { get; set; }
        public byte ControllerNumber { get; set; }
        public int DeviceIndex { get; set; }
        public string DeviceName { get; set; }
        
        // Display properties
        public string DisplayType => MessageType.ToString();
        public string DisplayChannel => (Channel + 1).ToString();
        public string DisplayValue => MessageType == MidiMessageType.Note 
            ? $"Note: {NoteNumber}" 
            : MessageType == MidiMessageType.ControlChange 
                ? $"CC: {ControllerNumber}" 
                : "Pitch Bend";

        public MultiMapping(MidiMapping mapping)
        {
            ControllerInput = mapping.ControllerInput;
            MessageType = mapping.MessageType;
            Channel = mapping.Channel;
            NoteNumber = mapping.NoteNumber;
            ControllerNumber = mapping.ControllerNumber;
            DeviceIndex = mapping.MidiDeviceIndex;
            DeviceName = mapping.MidiDeviceName;
        }
    }
}
