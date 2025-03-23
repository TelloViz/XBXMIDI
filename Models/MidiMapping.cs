using SharpDX.XInput;
using System.Text.Json.Serialization;
using System;
using System.Text.Json;
using System.IO;  // Add this for File operations
using System.Collections.Generic;

namespace XB2Midi.Models
{
    public enum MidiMessageType
    {
        Note,
        ControlChange,
        PitchBend
    }

    public class MidiMapping
    {
        public string ControllerInput { get; set; } = string.Empty;
        public MidiMessageType MessageType { get; set; }
        public byte Channel { get; set; }
        public byte NoteNumber { get; set; }
        public byte ControllerNumber { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
    }

    public class MappingConfiguration
    {
        public List<MidiMapping> Mappings { get; set; } = new();

        public void SaveToFile(string filepath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filepath, JsonSerializer.Serialize(this, options));
        }

        public static MappingConfiguration LoadFromFile(string filepath)
        {
            if (!File.Exists(filepath))
                return new MappingConfiguration();

            string jsonString = File.ReadAllText(filepath);
            return JsonSerializer.Deserialize<MappingConfiguration>(jsonString) 
                ?? new MappingConfiguration();
        }
    }
}