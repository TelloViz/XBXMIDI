using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    /// <summary>
    /// Specialized mapping manager for Multi Mode which allows multiple mappings per controller input
    /// </summary>
    public class MultiMappingManager : MappingManagerBase
    {
        /// <summary>
        /// Constructor requiring a MIDI output device
        /// </summary>
        /// <param name="output">MIDI output device</param>
        public MultiMappingManager(MidiOutput output) : base(output)
        {
        }

        /// <summary>
        /// Gets the file extension for Multi mode mappings
        /// </summary>
        /// <returns>File extension</returns>
        protected override string GetFileExtension() {
            return ".multi.json";
        }

        /// <summary>
        /// Adds a new mapping, ensuring it's set to Multi mode
        /// </summary>
        /// <param name="mapping">Mapping to add</param>
        public override void AddMapping(MidiMapping mapping) {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Ensure mapping is for Multi mode
            mapping.Mode = MappingMode.Multi;
            
            // In Multi mode, we don't remove existing mappings for the same controller input
            // Instead, we check if this exact mapping already exists to avoid duplicates
            var existingMapping = mappings.FirstOrDefault(m => AreMappingsEqual(m, mapping));
            
            if (existingMapping == null)
            {
                mappings.Add(mapping);
                NotifyMappingsChanged();
                OnMappingEvent($"Added Multi mapping: {mapping.ControllerInput} -> {mapping.MessageType}");
            }
            else
            {
                OnMappingEvent($"Duplicate mapping not added: {mapping.ControllerInput} -> {mapping.MessageType}");
            }
        }

        /// <summary>
        /// Removes a specific mapping
        /// </summary>
        /// <param name="mapping">Mapping to remove</param>
        public override void RemoveMapping(MidiMapping mapping) {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Find the exact mapping to remove
            var mappingToRemove = mappings.FirstOrDefault(m => AreMappingsEqual(m, mapping));
            
            if (mappingToRemove != null)
            {
                mappings.Remove(mappingToRemove);
                NotifyMappingsChanged();
                OnMappingEvent($"Removed Multi mapping: {mapping.ControllerInput} -> {mapping.MessageType}");
            }
        }

        /// <summary>
        /// Checks if two mappings are functionally equivalent
        /// </summary>
        private bool AreMappingsEqual(MidiMapping x, MidiMapping y) {
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

        /// <summary>
        /// Gets all mappings for a specific controller input
        /// </summary>
        /// <param name="controllerInput">The controller input to find mappings for</param>
        /// <returns>List of mappings for the specified controller input</returns>
        public List<MidiMapping> GetMappingsForInput(string controllerInput) {
            if (string.IsNullOrEmpty(controllerInput))
                return new List<MidiMapping>();

            return mappings
                .Where(m => string.Equals(m.ControllerInput, controllerInput, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Handles controller input events for Multi mode
        /// </summary>
        /// <param name="e">Controller input event args</param>
        public override void HandleControllerInput(ControllerInputEventArgs e) {
            if (string.IsNullOrEmpty(e.InputName) || midiOutput == null)
                return;

            // Get all mappings for this input
            var inputMappings = GetMappingsForInput(e.InputName);
            
            if (inputMappings.Count == 0)
                return;

            // Process each mapping in order
            foreach (var mapping in inputMappings)
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
        }

        private void HandleNoteMessage(MidiMapping mapping, ControllerInputEventArgs e) {
            if (e.InputType == ControllerInputType.Button)
            {
                bool isPressed = Convert.ToBoolean(e.Value);
                if (isPressed)
                {
                    midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                        mapping.NoteNumber, 127);
                    OnMappingEvent($"Multi Note On: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
                else
                {
                    midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, 
                                         mapping.NoteNumber);
                    OnMappingEvent($"Multi Note Off: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
            }
            else if (e.InputType == ControllerInputType.Thumbstick)
            {
                try
                {
                    // For Multi mode, we want to handle both axes of thumbsticks
                    float normalizedValue;
                    
                    // Handle combined thumbstick value
                    if (e.Value is dynamic)
                    {
                        dynamic stickValue = e.Value;
                        
                        // Check if we have X/Y values
                        if (e.InputName.EndsWith("X", StringComparison.OrdinalIgnoreCase))
                        {
                            // Map X value (-32768 to 32767) to MIDI velocity (0-127)
                            short xValue = stickValue.X;
                            normalizedValue = (xValue + 32768f) / 65535f;
                        }
                        else if (e.InputName.EndsWith("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            // Map Y value (-32768 to 32767) to MIDI velocity (0-127)
                            short yValue = stickValue.Y;
                            normalizedValue = (yValue + 32768f) / 65535f;
                        }
                        else
                        {
                            // Use magnitude for non-specific axis inputs
                            short xValue = stickValue.X;
                            short yValue = stickValue.Y;
                            double magnitude = Math.Sqrt(xValue * xValue + yValue * yValue);
                            normalizedValue = (float)(magnitude / 32768f);
                        }
                    }
                    else
                    {
                        // Handle single-axis value (-1.0 to 1.0)
                        normalizedValue = (float)e.Value;
                        normalizedValue = (normalizedValue + 1.0f) / 2.0f;
                    }
                    
                    byte velocity = (byte)Math.Clamp(normalizedValue * 127, 0, 127);
                    
                    // Send note with velocity
                    midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                        mapping.NoteNumber, velocity);
                    OnMappingEvent($"Multi Note (Continuous): Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}, Velocity {velocity}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling thumbstick note in Multi mode: {ex.Message}");
                }
            }
            else if (e.InputType == ControllerInputType.Trigger)
            {
                try
                {
                    // Triggers typically give values from 0.0 to 1.0
                    float normalizedValue = (float)e.Value;
                    byte velocity = (byte)Math.Clamp(normalizedValue * 127, 0, 127);
                    
                    // Only send note on if trigger is pressed enough
                    if (velocity > 0)
                    {
                        midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                            mapping.NoteNumber, velocity);
                        OnMappingEvent($"Multi Note (Trigger): Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}, Velocity {velocity}");
                    }
                    else
                    {
                        midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, 
                                             mapping.NoteNumber);
                        OnMappingEvent($"Multi Note Off (Trigger): Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling trigger note in Multi mode: {ex.Message}");
                }
            }
        }

        private void HandleControlChangeMessage(MidiMapping mapping, ControllerInputEventArgs e) {
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
                    // Handle thumbstick similar to note handling
                    float normalizedValue;
                    
                    if (e.Value is dynamic)
                    {
                        dynamic stickValue = e.Value;
                        
                        if (e.InputName.EndsWith("X", StringComparison.OrdinalIgnoreCase))
                        {
                            short xValue = stickValue.X;
                            normalizedValue = (xValue + 32768f) / 65535f;
                        }
                        else if (e.InputName.EndsWith("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            short yValue = stickValue.Y;
                            normalizedValue = (yValue + 32768f) / 65535f;
                        }
                        else
                        {
                            // Use magnitude for non-specific axis
                            short xValue = stickValue.X;
                            short yValue = stickValue.Y;
                            double magnitude = Math.Sqrt(xValue * xValue + yValue * yValue);
                            normalizedValue = (float)(magnitude / 32768f);
                        }
                    }
                    else
                    {
                        normalizedValue = (float)e.Value;
                        normalizedValue = (normalizedValue + 1.0f) / 2.0f;
                    }
                    
                    ccValue = (byte)Math.Clamp(normalizedValue * 127, 0, 127);
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
                OnMappingEvent($"Multi CC: Channel {mapping.Channel + 1}, Control {mapping.ControllerNumber}, Value {ccValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling control change in Multi mode: {ex.Message}");
            }
        }

        private void HandlePitchBendMessage(MidiMapping mapping, ControllerInputEventArgs e) {
            try
            {
                int pitchValue;
                
                if (e.InputType == ControllerInputType.Button)
                {
                    // Button is either centered (8192) or max (16383)
                    pitchValue = Convert.ToBoolean(e.Value) ? 16383 : 8192;
                }
                else if (e.InputType == ControllerInputType.Thumbstick)
                {
                    // Handle thumbstick similar to other handlers
                    float normalizedValue;
                    
                    if (e.Value is dynamic)
                    {
                        dynamic stickValue = e.Value;
                        
                        if (e.InputName.EndsWith("X", StringComparison.OrdinalIgnoreCase))
                        {
                            short xValue = stickValue.X;
                            normalizedValue = (xValue + 32768f) / 65535f;
                        }
                        else if (e.InputName.EndsWith("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            short yValue = stickValue.Y;
                            normalizedValue = (yValue + 32768f) / 65535f;
                        }
                        else
                        {
                            // Use magnitude for non-specific axis
                            short xValue = stickValue.X;
                            short yValue = stickValue.Y;
                            double magnitude = Math.Sqrt(xValue * xValue + yValue * yValue);
                            normalizedValue = (float)(magnitude / 32768f);
                        }
                    }
                    else
                    {
                        normalizedValue = (float)e.Value;
                        normalizedValue = (normalizedValue + 1.0f) / 2.0f;
                    }
                    
                    pitchValue = (int)Math.Clamp(normalizedValue * 16383, 0, 16383);
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
                OnMappingEvent($"Multi Pitch Bend: Channel {mapping.Channel + 1}, Value {pitchValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling pitch bend in Multi mode: {ex.Message}");
            }
        }
    }
}