using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    public class MappingManager
    {
        private readonly MidiOutput midiOutput;
        private readonly List<MidiMapping> mappings;

        public MappingManager(MidiOutput output)
        {
            midiOutput = output;
            mappings = new List<MidiMapping>();
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HandleMapping: {ex.Message}");
                throw; // Rethrow to let caller handle it
            }
        }

        public void HandleControllerInput(ControllerInputEventArgs args)
        {
            var mapping = mappings.Find(m => m.ControllerInput == args.InputName);
            if (mapping == null) return;

            switch (mapping.MessageType)
            {
                case MidiMessageType.Note:
                    HandleNoteMapping(mapping, args.Value);
                    break;
                case MidiMessageType.ControlChange:
                    HandleControlChangeMapping(mapping, args.Value);
                    break;
                case MidiMessageType.PitchBend:
                    HandlePitchBendMapping(mapping, args.Value);
                    break;
            }
        }

        private void HandleNoteMapping(MidiMapping mapping, object value)
        {
            bool isPressed = Convert.ToInt32(value) != 0;
            if (isPressed)
            {
                midiOutput.SendNoteOn(mapping.Channel, mapping.NoteNumber, 127);
            }
            else
            {
                midiOutput.SendNoteOff(mapping.Channel, mapping.NoteNumber);
            }
        }

        private void HandleControlChangeMapping(MidiMapping mapping, object value)
        {
            int intValue = Convert.ToInt32(value);
            byte scaled = (byte)(intValue * 127 / mapping.MaxValue);
            midiOutput.SendControlChange(mapping.Channel, mapping.ControllerNumber, scaled);
        }

        private void HandlePitchBendMapping(MidiMapping mapping, object value)
        {
            int intValue = Convert.ToInt32(value);
            int scaled = (intValue * 16383 / mapping.MaxValue) - 8192;
            midiOutput.SendPitchBend(mapping.Channel, scaled);
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
                }
            }
        }

        public void SaveMappings(string filePath)
        {
            string jsonString = JsonSerializer.Serialize(mappings);
            File.WriteAllText(filePath, jsonString);
        }
    }
}