// Used by:
// - BasicMappingView.xaml.cs
// - ChordMappingView.xaml.cs
// - MainWindow.xaml.cs
// - MappingsViewControl.cs

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
        private List<MidiMapping> mappings = new List<MidiMapping>(); // List of MIDI mappings
        private MidiOutput midiOutput; // MIDI output object for sending messages
        private Dictionary<MappingMode, object> modeMappings = new Dictionary<MappingMode, object>(); // Dictionary for mode mappings
        private List<ChordModeMapping> chordMappings = new List<ChordModeMapping>(); // List of chord mode mappings

        public event EventHandler? MappingsChanged; // Event triggered when mappings change
        public event EventHandler<ChordModeMapping>? ChordMappingsLoaded; // Event triggered when chord mappings are loaded

        public delegate void MappingMessageHandler(string message); // Delegate for handling mapping messages
        private MappingMessageHandler? mappingMessageHandler; // Handler for mapping messages

        /// <summary>
        /// Initializes a new instance of the MappingManager class.
        /// This constructor takes a MidiOutput object as a parameter to send MIDI messages.
        /// </summary>
        /// <param name="output"></param>
        public MappingManager(MidiOutput output)
        {
            midiOutput = output; // Initialize the MIDI output object
        }

        /// <summary>
        /// Registers a mapping event handler.
        /// This method allows external classes to register a handler for mapping events.
        /// </summary>
        /// <param name="handler"></param>
        public void RegisterMappingEventHandler(MappingMessageHandler handler)
        {
            mappingMessageHandler = handler; // Assign the handler to the mappingMessageHandler delegate
        }

        /// <summary>
        /// Triggers a mapping event with a message.
        /// This method is used to notify registered handlers about mapping events.
        /// </summary>
        /// <param name="message"></param>
        protected void OnMappingEvent(string message)
        {
            mappingMessageHandler?.Invoke(message); // Invoke the handler with the message
        }

        /// <summary>
        /// Gets the current mappings.
        /// This method returns a list of all current MIDI mappings.
        /// </summary>
        /// <returns></returns>
        public List<MidiMapping> GetCurrentMappings()
        {
            return mappings.ToList();
        }

        /// <summary>
        /// Adds a new mapping to the list of mappings.
        /// This method allows external classes to add a new MIDI mapping.
        /// </summary>
        /// <param name="mapping"></param>
        public void AddMapping(MidiMapping mapping)
        {
            mappings.Add(mapping); // Add the new mapping to the list
            MappingsChanged?.Invoke(this, EventArgs.Empty); // Trigger the MappingsChanged event
        }

        /// <summary>
        /// Removes a mapping from the list of mappings.
        /// This method allows external classes to remove a MIDI mapping.
        /// </summary>
        /// <param name="mapping"></param>
        public void RemoveMapping(MidiMapping mapping)
        {
            mappings.Remove(mapping); // Remove the mapping from the list
            MappingsChanged?.Invoke(this, EventArgs.Empty); // Trigger the MappingsChanged event
        }

        /// <summary>
        /// Gets the mapping for a specific controller input.
        /// This method searches the list of mappings for a specific 
        /// controller input name and returns the corresponding mapping.
        /// </summary>
        /// <param name="controllerInput"></param>
        /// <returns></returns>
        public MidiMapping? GetControllerMapping(string controllerInput)
        {
            if (controllerInput == null) // Check if the input is null
                return null;
                
            // Debug.WriteLine($"Looking up mapping for: '{controllerInput}'"); // Commented out to reduce processor load
                
            var mapping = mappings.FirstOrDefault(m => // Search for a mapping with the same controller input
                string.Equals(m.ControllerInput, controllerInput, StringComparison.OrdinalIgnoreCase)); // Case-insensitive comparison
                
            // Commented out to reduce processor load
            // if (mapping != null) // Check if a mapping was found
            // {
            //     Debug.WriteLine($"Found mapping: {mapping.ControllerInput} -> {mapping.MessageType}");
            // }
            // else 
            // {
            //     Debug.WriteLine($"No mapping found for: {controllerInput}");
            // }
                
            return mapping; // Return the found mapping or null if not found
        }

        /// <summary>
        /// Saves the current mappings to a file.
        /// This method serializes the current mappings to JSON format and writes them to a file.
        /// </summary>
        /// <param name="filePath"></param>
        public void SaveMappings(string filePath)
        {
            var allMappings = new // Create a new object to hold all mappings
            {
                BasicMappings = mappings, // Basic mappings
                ChordMappings = chordMappings, // Chord mappings
                // Other mapping types can be added here
            };

            string json = System.Text.Json.JsonSerializer.Serialize(allMappings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }); // Serialize to JSON with indentation
            File.WriteAllText(filePath, json); // Write the JSON to the specified file
        }

        /// <summary>
        /// Loads mappings from a file.
        /// This method reads a JSON file and deserializes it into the current mappings.
        /// </summary>
        /// <param name="filePath"></param>
        /// <exception cref="FileNotFoundException"></exception>
        public void LoadMappings(string filePath)
        {
            if (!File.Exists(filePath)) // Check if the file exists
                throw new FileNotFoundException($"Mapping file not found: {filePath}"); // Throw an exception if not found
            
            string json = File.ReadAllText(filePath); // Read the JSON from the file
            
            try
            {
                // Try to deserialize using the existing System.Text.Json approach
                using (JsonDocument document = JsonDocument.Parse(json)) // Parse the JSON document
                {
                    // Check if it has the new format with separate mapping types
                    if (document.RootElement.TryGetProperty("BasicMappings", out JsonElement basicMappingsElement)) // Check for BasicMappings property
                    {
                        // Load basic mappings
                        mappings = System.Text.Json.JsonSerializer.Deserialize<List<MidiMapping>>(basicMappingsElement.GetRawText()) ?? new List<MidiMapping>(); // Deserialize to List<MidiMapping>
                        
                        // Load chord mappings if available
                        if (document.RootElement.TryGetProperty("ChordMappings", out JsonElement chordMappingsElement)) // Check for ChordMappings property
                        {
                            chordMappings = System.Text.Json.JsonSerializer.Deserialize<List<ChordModeMapping>>(chordMappingsElement.GetRawText()) ?? new List<ChordModeMapping>(); // Deserialize to List<ChordModeMapping>
                            
                            if (chordMappings.Count > 0) // Check if any chord mappings were found
                            {
                                ChordMappingsLoaded?.Invoke(this, chordMappings[0]); // Trigger the ChordMappingsLoaded event with the first mapping
                            }
                        }
                    } 
                    else
                    {
                        // Fall back to legacy format (just basic mappings)
                        mappings = System.Text.Json.JsonSerializer.Deserialize<List<MidiMapping>>(json); // Deserialize to List<MidiMapping>
                    }
                }
            }
            catch
            {
                // Final fallback - try direct deserialization
                mappings = System.Text.Json.JsonSerializer.Deserialize<List<MidiMapping>>(json) ?? new List<MidiMapping>(); // Deserialize to List<MidiMapping>
            }
            
            MappingsChanged?.Invoke(this, EventArgs.Empty); // Trigger the MappingsChanged event
        }

        /// <summary>
        /// Saves a chord mapping to the list of chord mappings.
        /// This method allows external classes to save a new chord mapping.
        /// </summary>
        /// <param name="mapping"></param>
        public void SaveChordMapping(ChordModeMapping mapping)
        {
            // Remove any existing mapping with the same name
            chordMappings.RemoveAll(m => m.Name == mapping.Name);
            
            // Add the new mapping
            chordMappings.Add(mapping);
        }

        /// <summary>
        /// Gets all chord mappings.
        /// This method returns a list of all chord mappings.
        /// </summary>
        /// <returns>List of ChordModeMapping</returns>
        public List<ChordModeMapping> GetAllChordMappings()
        {
            return new List<ChordModeMapping>(chordMappings); // Return a copy of the list of chord mappings
        }

        /// <summary>
        /// Saves the chord mapping to the specified state.
        /// This method allows external classes to save the current chord mapping to a specific state.
        /// </summary>
        /// <param name="state"></param>
        public void SaveChordMapping(ModeState state)
        {
            var chordMapping = new ChordModeMapping(state); // Create a new ChordModeMapping from the state
            modeMappings[MappingMode.Chord] = chordMapping; // Save the mapping to the modeMappings dictionary
        }

        /// <summary>
        /// Loads the chord mapping from the specified state.
        /// This method allows external classes to load the current chord mapping from a specific state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool LoadChordMapping(ModeState state)
        {
            if (chordMappings.Count > 0) // Check if there are any chord mappings
            {
                chordMappings[0].ApplyTo(state); // Apply the first chord mapping to the state
                return true; // Return true if successful
            }
            return false; // Return false if no chord mappings were found
        }

        /// <summary>
        /// Handles controller input events and sends corresponding MIDI messages.
        /// This method is called when a controller input event occurs.
        /// It checks if the input name matches any of the mappings and sends the appropriate MIDI message.
        /// It also handles different message types such as Note, Control Change, and Pitch Bend.
        /// </summary>
        /// <param name="e">The event arguments containing the input name and value.</param>
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            var mapping = GetControllerMapping(e.InputName); // Get the mapping for the input name
            if (mapping != null && midiOutput != null) // Check if mapping exists and midiOutput is initialized
            {
                switch (mapping.MessageType) // Check the message type of the mapping
                {
                    case MidiMessageType.Note: // Handle Note message type
                        bool isPressed = Convert.ToBoolean(e.Value); // Convert value to boolean
                        if (isPressed) // Check if the button is pressed
                        {
                            midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, 127); // Send Note On message, velocity 127
                            // Debug.WriteLine($"Note On: {mapping.NoteNumber} on channel {mapping.Channel}"); // Commented out to reduce processor load
                        }
                        else
                        {
                            midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber); // Send Note Off message
                            // Debug.WriteLine($"Note Off: {mapping.NoteNumber} on channel {mapping.Channel}"); // Commented out to reduce processor load 
                        }
                        break;

                    case MidiMessageType.ControlChange: // Handle Control Change message type
                        byte controlValue = Convert.ToByte(e.Value); // Convert value to byte
                        midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, mapping.ControllerNumber, controlValue); // Send Control Change message
                        // Debug.WriteLine($"Control Change: {mapping.ControllerNumber} = {controlValue}"); // Commented out to reduce processor load
                        break;

                    case MidiMessageType.PitchBend: // Handle Pitch Bend message type

                        short pitchValue; // Initialize pitchValue
                        
                        if (e.Value is short shortValue) // Check if the value is a short
                        {
                            
                            pitchValue = (short)((shortValue + 32768) / 4); // Map from -32768 to 32767 to 0 to 16383
                        }
                        else
                        {
                            try 
                            {
                                dynamic dynamicValue = e.Value; // Use dynamic to handle different types
                                if (dynamicValue != null) // Check if dynamicValue is not null
                                {
                                    short xValue = Convert.ToInt16(dynamicValue); // Convert to short
                                    pitchValue = (short)((xValue + 32768) / 4); // Map from -32768 to 32767 to 0 to 16383
                                }
                                else
                                {
                                    pitchValue = 8192; // Default to center pitch
                                }
                            }
                            catch
                            {
                                pitchValue = 8192; // Default to center pitch if conversion fails ( TODO: Not good to use catch for control flow)
                            }
                        }
                        
                        pitchValue = (short)Math.Clamp((int)pitchValue, 0, 16383); // Ensure pitchValue is within valid range
                        
                        midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchValue); // Send Pitch Bend message
                        // Debug.WriteLine($"Pitch Bend: {pitchValue}"); // Commented out to reduce processor load

                        break;
                }
            }
        }
    }

    /// <summary>
    /// Represents the data structure for mapping files.
    /// This class is used to deserialize the JSON mapping files into C# objects.
    /// </summary>
    public class MappingFileData
    {
        public List<MidiMapping>? BasicMappings { get; set; } // List of basic MIDI mappings
        public ChordModeMapping? ChordMapping { get; set; } // Chord mode mapping
        public List<ChordModeMapping>? ChordMappings { get; set; } // List of chord mode mappings
        public List<ModeState>? ModeStates { get; set; } // List of mode states
    }
}