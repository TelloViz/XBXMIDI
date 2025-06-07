using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    /// <summary>
    /// Specialized mapping manager for Arpeggio Mode
    /// Note: This is a placeholder implementation. Specific arpeggio functionality will be added later.
    /// </summary>
    public class ArpeggioMappingManager : MappingManagerBase
    {
        // Add arpeggio-specific properties here later
        private List<ArpeggioPattern> _arpeggioPatterns = new List<ArpeggioPattern>();
        
        /// <summary>
        /// Constructor requiring a MIDI output device
        /// </summary>
        /// <param name="output">MIDI output device</param>
        public ArpeggioMappingManager(MidiOutput output) : base(output)
        {
        }

        /// <summary>
        /// Gets the file extension for Arpeggio mode mappings
        /// </summary>
        /// <returns>File extension</returns>
        protected override string GetFileExtension()
        {
            return ".arpeggio.json";
        }

        /// <summary>
        /// Adds a new mapping, ensuring it's set to Arpeggio mode
        /// </summary>
        /// <param name="mapping">Mapping to add</param>
        public override void AddMapping(MidiMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Ensure mapping is for Arpeggio mode
            mapping.Mode = MappingMode.Arpeggio;
            
            // Remove any existing mapping for this controller input
            mappings.RemoveAll(m => string.Equals(m.ControllerInput, mapping.ControllerInput, 
                                                StringComparison.OrdinalIgnoreCase));
            
            mappings.Add(mapping);
            NotifyMappingsChanged();
            OnMappingEvent($"Added arpeggio mapping: {mapping.ControllerInput}");
        }

        /// <summary>
        /// Handles controller input events for Arpeggio mode (placeholder)
        /// </summary>
        /// <param name="e">Controller input event args</param>
        public override void HandleControllerInput(ControllerInputEventArgs e)
        {
            // For now, just log the input
            OnMappingEvent($"Arpeggio mode received input: {e.InputName} = {e.Value}");
            
            // Actual arpeggio handling will be implemented later
            // This would involve:
            // 1. Triggering arpeggios based on button presses
            // 2. Modifying arpeggio parameters based on thumbsticks/triggers
            // 3. Managing arpeggio timing, pattern selection, etc.
            
            // For now, we'll just pass through any direct mappings
            var mapping = GetControllerMapping(e.InputName);
            if (mapping != null && midiOutput != null)
            {
                switch (mapping.MessageType)
                {
                    case MidiMessageType.Note:
                        HandleBasicNoteMessage(mapping, e);
                        break;
                        
                    case MidiMessageType.ControlChange:
                        HandleBasicControlChangeMessage(mapping, e);
                        break;
                        
                    case MidiMessageType.PitchBend:
                        HandleBasicPitchBendMessage(mapping, e);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Placeholder for future arpeggio pattern functionality
        /// </summary>
        /// <param name="pattern">Arpeggio pattern to add</param>
        public void AddArpeggioPattern(ArpeggioPattern pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));
                
            _arpeggioPatterns.Add(pattern);
            OnMappingEvent($"Added arpeggio pattern: {pattern.Name}");
        }
        
        /// <summary>
        /// Override SaveMappings to include arpeggio patterns
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

            // Create a container with both mappings and patterns
            var container = new ArpeggioMappingContainer
            {
                FileVersion = "1.0",
                MidiMappings = mappings,
                ArpeggioPatterns = _arpeggioPatterns
            };

            // Serialize and save
            string json = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            
            OnMappingEvent($"Arpeggio mappings saved to {filePath}");
        }
        
        /// <summary>
        /// Override LoadMappings to handle arpeggio patterns
        /// </summary>
        /// <param name="filePath">Path to load from</param>
        public override void LoadMappings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Mapping file not found: {filePath}");

            string json = File.ReadAllText(filePath);
            
            try
            {
                // Try to deserialize as a container
                var container = JsonSerializer.Deserialize<ArpeggioMappingContainer>(json);
                
                if (container != null)
                {
                    // Load MIDI mappings
                    if (container.MidiMappings != null)
                    {
                        mappings.Clear();
                        mappings.AddRange(container.MidiMappings);
                    }
                    
                    // Load arpeggio patterns
                    if (container.ArpeggioPatterns != null)
                    {
                        _arpeggioPatterns.Clear();
                        _arpeggioPatterns.AddRange(container.ArpeggioPatterns);
                    }
                    
                    NotifyMappingsChanged();
                    OnMappingEvent($"Arpeggio mappings loaded from {filePath}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading arpeggio mappings: {ex.Message}");
                throw new FormatException($"The selected file doesn't contain valid arpeggio mappings: {ex.Message}");
            }
            
            throw new FormatException("The selected file doesn't contain valid arpeggio mappings.");
        }
        
        // Basic handlers (just placeholders, arpeggio mode will have more sophisticated handling)
        private void HandleBasicNoteMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            if (e.InputType == ControllerInputType.Button)
            {
                bool isPressed = Convert.ToBoolean(e.Value);
                if (isPressed)
                {
                    midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                        mapping.NoteNumber, 127);
                    OnMappingEvent($"Arpeggio Note On: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
                else
                {
                    midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, 
                                         mapping.NoteNumber);
                    OnMappingEvent($"Arpeggio Note Off: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
            }
        }
        
        private void HandleBasicControlChangeMessage(MidiMapping mapping, ControllerInputEventArgs e)
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
                OnMappingEvent($"Arpeggio CC: Channel {mapping.Channel + 1}, Control {mapping.ControllerNumber}, Value {ccValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling control change: {ex.Message}");
            }
        }
        
        private void HandleBasicPitchBendMessage(MidiMapping mapping, ControllerInputEventArgs e)
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
                OnMappingEvent($"Arpeggio Pitch Bend: Channel {mapping.Channel + 1}, Value {pitchValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling pitch bend: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Placeholder for future arpeggio pattern class
    /// </summary>
    public class ArpeggioPattern
    {
        public string Name { get; set; } = "Default Pattern";
        public int[] Steps { get; set; } = new int[] { 0, 4, 7, 12 }; // Default to a major chord
        public int Rate { get; set; } = 8; // Eighth notes
        public bool Ascending { get; set; } = true;
        
        // Add more arpeggio-specific properties as needed
    }
    
    /// <summary>
    /// Container class for arpeggio mappings file
    /// </summary>
    public class ArpeggioMappingContainer
    {
        public string FileVersion { get; set; } = "1.0";
        public List<MidiMapping> MidiMappings { get; set; } = new List<MidiMapping>();
        public List<ArpeggioPattern> ArpeggioPatterns { get; set; } = new List<ArpeggioPattern>();
    }
}