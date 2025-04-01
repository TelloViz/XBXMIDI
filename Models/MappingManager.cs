using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    public class MappingManager
    {
        private ControllerMode currentMode = ControllerMode.Direct;
        public event EventHandler<ControllerMode>? ModeChanged;
        public event EventHandler<EventArgs>? MappingsChanged;

        private readonly MidiOutput midiOutput;
        private readonly List<MidiMapping> mappings;

        public MappingManager(MidiOutput output)
        {
            midiOutput = output;
            mappings = new List<MidiMapping>();
        }

        public ControllerMode CurrentMode => currentMode;

        public void CycleMode(bool forward)
        {
            var modes = Enum.GetValues<ControllerMode>();
            int currentIndex = (int)currentMode;
            
            if (forward)
            {
                currentIndex = (currentIndex + 1) % modes.Length;
            }
            else
            {
                currentIndex = (currentIndex - 1 + modes.Length) % modes.Length;
            }

            currentMode = (ControllerMode)currentIndex;
            UpdateLEDForMode(currentMode);
        }

        public void HandleMapping(MidiMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            try
            {
                // Remove any existing mapping for this controller input
                mappings.RemoveAll(m => m.ControllerInput == mapping.ControllerInput);
                
                // Add the new mapping
                mappings.Add(mapping);
                
                System.Diagnostics.Debug.WriteLine($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType}");
                NotifyMappingsChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HandleMapping: {ex.Message}");
                throw; // Rethrow to let caller handle it
            }
        }

        public void HandleControllerInput(ControllerInputEventArgs args)
        {
            // Handle Back/Start buttons for mode cycling
            if (args.InputType == ControllerInputType.Button)
            {
                if (args.InputName == "Back" && Convert.ToInt32(args.Value) == 1)
                {
                    CycleMode(false);
                    return;
                }
                else if (args.InputName == "Start" && Convert.ToInt32(args.Value) == 1)
                {
                    CycleMode(true);
                    return;
                }
            }

            // Only process other inputs if in Direct mode for now
            if (currentMode == ControllerMode.Direct)
            {
                if (args.InputType == ControllerInputType.Thumbstick)
                {
                    // Handle X and Y axes separately
                    dynamic stickValue = args.Value;
                    string baseName = args.InputName;
                    
                    // Send X axis value
                    var xMapping = mappings.FirstOrDefault(m => m.ControllerInput == $"{baseName}X");
                    if (xMapping != null)
                    {
                        HandleAxisValue(xMapping, stickValue.X);
                    }

                    // Send Y axis value
                    var yMapping = mappings.FirstOrDefault(m => m.ControllerInput == $"{baseName}Y");
                    if (yMapping != null)
                    {
                        HandleAxisValue(yMapping, stickValue.Y);
                    }
                }
                else
                {
                    // Handle other inputs (buttons, triggers) as before
                    var mapping = mappings.FirstOrDefault(m => m.ControllerInput == args.InputName);
                    if (mapping != null)
                    {
                        HandleInputValue(mapping, args.Value);
                    }
                }
            }
        }

        private void HandleAxisValue(MidiMapping mapping, short value)
        {
            try
            {
                switch (mapping.MessageType)
                {
                    case MidiMessageType.PitchBend:
                        int bendValue;
                        if (value == 0)
                        {
                            bendValue = 8192; // Center position
                        }
                        else
                        {
                            // Map from -32768..32767 to 0..16383
                            bendValue = (int)(((long)value + 32768L) * 16383L / 65535L);
                        }
                        
                        // Add safety check
                        bendValue = Math.Clamp(bendValue, 0, 16383);
                        
                        System.Diagnostics.Debug.WriteLine($"Sending PitchBend: {bendValue}");
                        midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, bendValue);
                        break;

                    // ... rest of the cases remain the same ...
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HandleAxisValue: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Value: {value}, MappingType: {mapping.MessageType}");
                // Don't rethrow - we want to handle errors gracefully
            }
        }

        private void HandleInputValue(MidiMapping mapping, object value)
        {
            switch (mapping.MessageType)
            {
                case MidiMessageType.Note:
                    HandleNoteMapping(mapping, value);
                    break;
                case MidiMessageType.ControlChange:
                    HandleControlChangeMapping(mapping, value);
                    break;
                case MidiMessageType.PitchBend:
                    HandlePitchBendMapping(mapping, value);
                    break;
            }
        }

        private void HandleNoteMapping(MidiMapping mapping, object value)
        {
            bool isPressed = Convert.ToInt32(value) != 0;
            if (isPressed)
            {
                midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, 127);
            }
            else
            {
                midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber);
            }
        }

        private void HandleControlChangeMapping(MidiMapping mapping, object value)
        {
            int intValue = Convert.ToInt32(value);
            byte scaled = (byte)(intValue * 127 / mapping.MaxValue);
            midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, mapping.ControllerNumber, scaled);
        }

        private void HandlePitchBendMapping(MidiMapping mapping, object value)
        {
            try
            {
                // Convert incoming value (0-255 from trigger) to pitch bend range (-8192 to 8191)
                byte byteValue = Convert.ToByte(value);
                int scaled = (int)((byteValue / 255.0) * 16383) - 8192;
                
                System.Diagnostics.Debug.WriteLine($"Pitch Bend: Input={byteValue}, Scaled={scaled}");
                midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, scaled + 8192); // Adjust to 0-16383 range
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HandlePitchBendMapping: {ex.Message}");
                throw;
            }
        }

        public void LoadMappings(string filePath)
        {
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);
                var loadedMappings = JsonSerializer.Deserialize<List<MidiMapping>>(jsonString);
                if (loadedMappings != null)
                {
                    mappings.Clear();
                    mappings.AddRange(loadedMappings);
                    NotifyMappingsChanged();
                }
            }
        }

        public void SaveMappings(string filePath)
        {
            string jsonString = JsonSerializer.Serialize(mappings);
            File.WriteAllText(filePath, jsonString);
        }

        public IEnumerable<MidiMapping> GetCurrentMappings()
        {
            return mappings.ToList();
        }

        private void NotifyMappingsChanged()
        {
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void HandleInput(string input, double value)
        {
            var mapping = mappings.FirstOrDefault(m => m.ControllerInput == input);
            if (mapping == null) return;

            // Convert the input value (0-1) to MIDI range based on message type
            int midiValue = mapping.MessageType switch
            {
                MidiMessageType.PitchBend => (int)(value * 16383),
                _ => (int)(value * 127)
            };

            switch (mapping.MessageType)
            {
                case MidiMessageType.Note:
                    if (value > 0)
                        midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, (byte)midiValue);
                    else
                        midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber);
                    break;

                case MidiMessageType.ControlChange:
                    midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, mapping.ControllerNumber, (byte)midiValue);
                    break;

                case MidiMessageType.PitchBend:
                    midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, midiValue);
                    break;
            }
        }

        private void UpdateLEDForMode(ControllerMode mode)
        {
            ModeChanged?.Invoke(this, mode);
        }

        public MidiMapping? GetControllerMapping(string controllerInput)
        {
            // Look up mapping for this input
            return mappings.FirstOrDefault(m => m.ControllerInput == controllerInput);
        }
    }
}