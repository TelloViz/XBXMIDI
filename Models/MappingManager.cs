using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace XB2Midi.Models
{
    public class MappingManager
    {
        private List<MidiMapping> mappings = new List<MidiMapping>();
        private MidiOutput midiOutput;
        private Dictionary<MappingMode, object> modeMappings = new Dictionary<MappingMode, object>();
        private List<ChordModeMapping> chordMappings = new List<ChordModeMapping>();

        public event EventHandler? MappingsChanged;
        public event EventHandler<ChordModeMapping>? ChordMappingsLoaded;

        public MappingManager(MidiOutput output)
        {
            midiOutput = output;
        }

        public List<MidiMapping> GetCurrentMappings()
        {
            return mappings.ToList();
        }

        public void AddMapping(MidiMapping mapping)
        {
            mappings.Add(mapping);
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveMapping(MidiMapping mapping)
        {
            mappings.Remove(mapping);
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public MidiMapping? GetControllerMapping(string controllerInput)
        {
            if (controllerInput == null)
                return null;
                
            // Log the lookup to help with debugging
            Debug.WriteLine($"Looking up mapping for: '{controllerInput}'");
                
            // Try to find an exact match
            var mapping = mappings.FirstOrDefault(m => 
                string.Equals(m.ControllerInput, controllerInput, StringComparison.OrdinalIgnoreCase));
                
            if (mapping != null)
            {
                Debug.WriteLine($"Found mapping: {mapping.ControllerInput} -> {mapping.MessageType}");
            }
            else 
            {
                Debug.WriteLine($"No mapping found for: {controllerInput}");
            }
                
            return mapping;
        }

        public void SaveMappings(string filePath)
        {
            var allMappings = new
            {
                BasicMappings = mappings,
                ChordMappings = chordMappings,
                // Other mapping types can be added here
            };

            string json = System.Text.Json.JsonSerializer.Serialize(allMappings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public void LoadMappings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Mapping file not found: {filePath}");
            
            string json = File.ReadAllText(filePath);
            
            try
            {
                // Try to deserialize using the existing System.Text.Json approach
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    // Check if it has the new format with separate mapping types
                    if (document.RootElement.TryGetProperty("BasicMappings", out JsonElement basicMappingsElement))
                    {
                        // Load basic mappings
                        mappings = System.Text.Json.JsonSerializer.Deserialize<List<MidiMapping>>(basicMappingsElement.GetRawText());
                        
                        // Load chord mappings if available
                        if (document.RootElement.TryGetProperty("ChordMappings", out JsonElement chordMappingsElement))
                        {
                            chordMappings = System.Text.Json.JsonSerializer.Deserialize<List<ChordModeMapping>>(chordMappingsElement.GetRawText());
                            
                            if (chordMappings.Count > 0)
                            {
                                ChordMappingsLoaded?.Invoke(this, chordMappings[0]);
                            }
                        }
                    }
                    else
                    {
                        // Fall back to legacy format (just basic mappings)
                        mappings = System.Text.Json.JsonSerializer.Deserialize<List<MidiMapping>>(json);
                    }
                }
            }
            catch
            {
                // Final fallback - try direct deserialization
                mappings = System.Text.Json.JsonSerializer.Deserialize<List<MidiMapping>>(json) ?? new List<MidiMapping>();
            }
            
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveChordMapping(ChordModeMapping mapping)
        {
            // Remove any existing mapping with the same name
            chordMappings.RemoveAll(m => m.Name == mapping.Name);
            
            // Add the new mapping
            chordMappings.Add(mapping);
        }

        public List<ChordModeMapping> GetAllChordMappings()
        {
            return new List<ChordModeMapping>(chordMappings);
        }

        public void SaveChordMapping(ModeState state)
        {
            var chordMapping = new ChordModeMapping(state);
            modeMappings[MappingMode.Chord] = chordMapping;
        }

        public bool LoadChordMapping(ModeState state)
        {
            if (chordMappings.Count > 0)
            {
                chordMappings[0].ApplyTo(state);
                return true;
            }
            return false;
        }

        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            var mapping = GetControllerMapping(e.InputName);
            if (mapping != null && midiOutput != null)
            {
                switch (mapping.MessageType)
                {
                    case MidiMessageType.Note:
                        bool isPressed = Convert.ToBoolean(e.Value);
                        if (isPressed)
                        {
                            midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, 127);
                            Debug.WriteLine($"Note On: {mapping.NoteNumber} on channel {mapping.Channel}");
                        }
                        else
                        {
                            midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber);
                            Debug.WriteLine($"Note Off: {mapping.NoteNumber} on channel {mapping.Channel}");
                        }
                        break;

                    case MidiMessageType.ControlChange:
                        byte controlValue = Convert.ToByte(e.Value);
                        midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, mapping.ControllerNumber, controlValue);
                        Debug.WriteLine($"Control Change: {mapping.ControllerNumber} = {controlValue}");
                        break;

                    case MidiMessageType.PitchBend:
                        // Convert value to pitch bend range (0-16383)
                        short pitchValue;
                        
                        if (e.Value is short shortValue)
                        {
                            // Map from -32768 to 32767 to 0 to 16383
                            pitchValue = (short)((shortValue + 32768) / 4);
                        }
                        else 
                        {
                            try 
                            {
                                // Try to extract X value using dynamic
                                dynamic dynamicValue = e.Value;
                                if (dynamicValue != null)
                                {
                                    short xValue = Convert.ToInt16(dynamicValue);
                                    pitchValue = (short)((xValue + 32768) / 4);
                                }
                                else
                                {
                                    // Default to center pitch
                                    pitchValue = 8192;
                                }
                            }
                            catch
                            {
                                // Default to center pitch if conversion fails
                                pitchValue = 8192;
                            }
                        }
                        
                        // Ensure value is in range - fix the ambiguous call by explicitly casting
                        pitchValue = (short)Math.Clamp((int)pitchValue, 0, 16383);
                        
                        midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchValue);
                        Debug.WriteLine($"Pitch Bend: {pitchValue}");
                        break;
                }
            }
        }
    }

    public class MappingFileData
    {
        public List<MidiMapping>? BasicMappings { get; set; }
        public ChordModeMapping? ChordMapping { get; set; }
        // Add other mode mappings as needed
    }
}