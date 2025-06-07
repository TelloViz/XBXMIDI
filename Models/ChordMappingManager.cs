using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    /// <summary>
    /// Manages mappings specifically for Chord Mode
    /// </summary>
    public class ChordMappingManager : MappingManagerBase
    {
        private List<ChordModeMapping> _chordMappings = new List<ChordModeMapping>();
        public event EventHandler<ChordModeMapping>? ChordMappingsLoaded;

        /// <summary>
        /// Constructor requiring a MIDI output device
        /// </summary>
        /// <param name="output">MIDI output device</param>
        public ChordMappingManager(MidiOutput output) : base(output)
        {
        }

        /// <summary>
        /// Gets the file extension for Chord mode mappings
        /// </summary>
        /// <returns>File extension</returns>
        protected override string GetFileExtension()
        {
            return ".chord.json";
        }

        /// <summary>
        /// Gets all chord mode mappings
        /// </summary>
        /// <returns>List of ChordModeMapping</returns>
        public List<ChordModeMapping> GetAllChordMappings()
        {
            return new List<ChordModeMapping>(_chordMappings);
        }

        /// <summary>
        /// Adds a new mapping, ensuring it's set to Chord mode
        /// </summary>
        /// <param name="mapping">Mapping to add</param>
        public override void AddMapping(MidiMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Ensure mapping is for Chord mode
            mapping.Mode = MappingMode.Chord;

            // Remove any existing mapping for this controller input
            mappings.RemoveAll(m => string.Equals(m.ControllerInput, mapping.ControllerInput,
                                                StringComparison.OrdinalIgnoreCase));

            mappings.Add(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Adds a chord mapping to the list of chord mappings
        /// </summary>
        /// <param name="mapping">ChordModeMapping to add</param>
        public void AddChordMapping(ChordModeMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Remove any existing mapping with the same name
            _chordMappings.RemoveAll(m => m.Name == mapping.Name);

            // Add the new mapping
            _chordMappings.Add(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Adds a chord mapping to the list of chord mappings
        /// </summary>
        /// <param name="mapping">ChordModeMapping to add</param>
        public void SaveChordMapping(ChordModeMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Remove any existing mapping with the same name
            _chordMappings.RemoveAll(m => m.Name == mapping.Name);

            // Add the new mapping
            _chordMappings.Add(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Loads chord mapping into state
        /// </summary>
        /// <param name="state">State to update</param>
        /// <returns>True if successful</returns>
        public bool LoadChordMapping(ModeState state)
        {
            if (_chordMappings.Count > 0)
            {
                _chordMappings[0].ApplyTo(state);
                ChordMappingsLoaded?.Invoke(this, _chordMappings[0]);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Saves both standard MIDI mappings and chord-specific mappings
        /// </summary>
        /// <param name="filePath">Path to save to</param>
        public override void SaveMappings(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            // Ensure the file has the correct extension
            string extension = GetFileExtension();
            if (!filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.ChangeExtension(filePath, extension);
            }

            // Create container with both types of mappings
            var container = new ChordMappingContainer
            {
                FileVersion = "1.0",
                MidiMappings = mappings,
                ChordMappings = _chordMappings  // Save all chord mappings
            };

            string json = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            
            OnMappingEvent($"Chord mappings saved to {filePath}");
        }

        /// <summary>
        /// Loads both standard MIDI mappings and chord-specific mappings
        /// </summary>
        /// <param name="filePath">Path to load from</param>
        public override void LoadMappings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Mapping file not found: {filePath}");

            string json = File.ReadAllText(filePath);

            try
            {
                // Try to deserialize as a container first
                var container = JsonSerializer.Deserialize<ChordMappingContainer>(json);

                if (container != null && container.MidiMappings != null)
                {
                    // Load standard MIDI mappings
                    mappings.Clear();
                    mappings.AddRange(container.MidiMappings);

                    // Load chord mappings if available
                    if (container.ChordMappings != null && container.ChordMappings.Count > 0)
                    {
                        _chordMappings.Clear();
                        _chordMappings.AddRange(container.ChordMappings);

                        // Notify listeners about the loaded chord mapping
                        ChordMappingsLoaded?.Invoke(this, _chordMappings[0]);
                    }

                    NotifyMappingsChanged();
                    return;
                }
            }
            catch (JsonException)
            {
                // If container deserialization fails, try legacy format
                try
                {
                    // Check if this is a legacy mapping file with BasicMappings and ChordMappings properties
                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        if (document.RootElement.TryGetProperty("ChordMappings", out JsonElement chordMappingsElement))
                        {
                            var loadedChordMappings = JsonSerializer.Deserialize<List<ChordModeMapping>>(
                                chordMappingsElement.GetRawText());

                            if (loadedChordMappings != null && loadedChordMappings.Count > 0)
                            {
                                _chordMappings.Clear();
                                _chordMappings.AddRange(loadedChordMappings);

                                // Notify listeners
                                ChordMappingsLoaded?.Invoke(this, _chordMappings[0]);
                            }
                        }

                        if (document.RootElement.TryGetProperty("BasicMappings", out JsonElement basicMappingsElement))
                        {
                            var loadedMappings = JsonSerializer.Deserialize<List<MidiMapping>>(
                                basicMappingsElement.GetRawText());

                            if (loadedMappings != null)
                            {
                                // Filter to only include Chord mode mappings
                                var chordMappings = loadedMappings
                                    .Where(m => m.Mode == MappingMode.Chord)
                                    .ToList();

                                if (chordMappings.Count > 0)
                                {
                                    mappings.Clear();
                                    mappings.AddRange(chordMappings);
                                }
                            }
                        }

                        NotifyMappingsChanged();
                        return;
                    }
                }
                catch
                {
                    // Last resort: try to deserialize as a plain list of MidiMappings
                    var loadedMappings = JsonSerializer.Deserialize<List<MidiMapping>>(json);
                    if (loadedMappings != null)
                    {
                        // Filter to only include Chord mode mappings
                        var chordMappings = loadedMappings
                            .Where(m => m.Mode == MappingMode.Chord)
                            .ToList();

                        if (chordMappings.Count > 0)
                        {
                            mappings.Clear();
                            mappings.AddRange(chordMappings);
                            NotifyMappingsChanged();
                            return;
                        }
                    }

                    throw new FormatException("The selected file doesn't contain any valid chord mappings.");
                }
            }

            throw new FormatException("The selected file doesn't contain any valid chord mappings.");
        }

        /// <summary>
        /// Updates the manager with a full set of chord mappings from the tab manager
        /// </summary>
        /// <param name="chordMappings">Collection of chord mappings</param>
        public void UpdateChordMappings(IEnumerable<ChordModeMapping> chordMappings)
        {
            if (chordMappings == null)
                throw new ArgumentNullException(nameof(chordMappings));
                
            _chordMappings.Clear();
            _chordMappings.AddRange(chordMappings);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Handles controller input events for Chord mode
        /// </summary>
        /// <param name="e">Controller input event args</param>
        public override void HandleControllerInput(ControllerInputEventArgs e)
        {
            // Standard MIDI mappings handling (similar to BasicMappingManager)
            var mapping = GetControllerMapping(e.InputName);
            if (mapping != null && midiOutput != null)
            {
                switch (mapping.MessageType)
                {
                    case MidiMessageType.Note:
                        HandleNoteMessage(mapping, e);
                        break;

                    case MidiMessageType.ControlChange:
                        HandleControlChangeMessage(mapping, e);
                        break;

                    case MidiMessageType.PitchBend:
                        HandlePitchBendMessage(mapping, e);
                        break;
                }
            }

            // For special chord-specific handling, the ModeState object
            // should be used directly through the ChordMappingView
        }

        private void HandleNoteMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            if (e.InputType == ControllerInputType.Button)
            {
                bool isPressed = Convert.ToBoolean(e.Value);
                if (isPressed)
                {
                    midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel,
                                        mapping.NoteNumber, 127);
                    OnMappingEvent($"Note On: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
                else
                {
                    midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel,
                                         mapping.NoteNumber);
                    OnMappingEvent($"Note Off: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
            }
            else if (e.InputType == ControllerInputType.Thumbstick || e.InputType == ControllerInputType.Trigger)
            {
                // In Chord mode, thumbsticks and triggers are typically handled by the ModeState
                // But we'll include basic handling here for completeness
                try
                {
                    float normalizedValue;
                    if (e.InputType == ControllerInputType.Thumbstick)
                    {
                        // Map thumbstick (typically -1.0 to 1.0) to MIDI velocity (0-127)
                        normalizedValue = (float)e.Value;
                        normalizedValue = (normalizedValue + 1.0f) / 2.0f; // Convert -1..1 to 0..1
                    }
                    else // Trigger
                    {
                        // Triggers typically give values from 0.0 to 1.0
                        normalizedValue = (float)e.Value;
                    }

                    byte velocity = (byte)Math.Clamp(normalizedValue * 127, 0, 127);

                    // Only send note on if value is significant
                    if (velocity > 5)
                    {
                        midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel,
                                            mapping.NoteNumber, velocity);
                        OnMappingEvent($"Note: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}, Velocity {velocity}");
                    }
                    else
                    {
                        midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel,
                                             mapping.NoteNumber);
                        OnMappingEvent($"Note Off: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling continuous controller for note: {ex.Message}");
                }
            }
        }

        private void HandleControlChangeMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            try
            {
                byte ccValue;

                if (e.InputType == ControllerInputType.Button)
                {
                    // Button is either 0 or 1, map to min/max CC value
                    ccValue = Convert.ToBoolean(e.Value) ? (byte)127 : (byte)0;
                }
                else if (e.InputType == ControllerInputType.Thumbstick)
                {
                    // Map thumbstick (-1.0 to 1.0) to CC value (0-127)
                    float normalizedValue = (float)e.Value;
                    ccValue = (byte)Math.Clamp(((normalizedValue + 1.0f) / 2.0f) * 127, 0, 127);
                }
                else if (e.InputType == ControllerInputType.Trigger)
                {
                    // Map trigger (0.0 to 1.0) to CC value (0-127)
                    float normalizedValue = (float)e.Value;
                    ccValue = (byte)Math.Clamp(normalizedValue * 127, 0, 127);
                }
                else
                {
                    // Default case
                    ccValue = 0;
                }

                midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel,
                                      mapping.ControllerNumber, ccValue);
                OnMappingEvent($"CC: Channel {mapping.Channel + 1}, Control {mapping.ControllerNumber}, Value {ccValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling control change: {ex.Message}");
            }
        }

        private void HandlePitchBendMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            try
            {
                // Map input value to pitch bend range (0-16383, with 8192 as center)
                int pitchValue;

                if (e.InputType == ControllerInputType.Button)
                {
                    // Button is either centered (8192) or max (16383)
                    pitchValue = Convert.ToBoolean(e.Value) ? 16383 : 8192;
                }
                else if (e.InputType == ControllerInputType.Thumbstick)
                {
                    // Map thumbstick (-1.0 to 1.0) to pitch bend (0-16383)
                    float normalizedValue = (float)e.Value;
                    pitchValue = (int)Math.Clamp(((normalizedValue + 1.0f) / 2.0f) * 16383, 0, 16383);
                }
                else if (e.InputType == ControllerInputType.Trigger)
                {
                    // Map trigger (0.0 to 1.0) to upper half of pitch bend (8192-16383)
                    float normalizedValue = (float)e.Value;
                    pitchValue = (int)Math.Clamp(8192 + (normalizedValue * 8191), 8192, 16383);
                }
                else
                {
                    // Default to center
                    pitchValue = 8192;
                }

                midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchValue);
                OnMappingEvent($"Pitch Bend: Channel {mapping.Channel + 1}, Value {pitchValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling pitch bend: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Container class for chord mappings file
    /// </summary>
    public class ChordMappingContainer
    {
        public string FileVersion { get; set; } = "1.0";
        public List<MidiMapping> MidiMappings { get; set; } = new List<MidiMapping>();
        public List<ChordModeMapping> ChordMappings { get; set; } = new List<ChordModeMapping>();
    }
}