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
        public byte Channel { get; set; } // Internal representation (0-15)
        public byte NoteNumber { get; set; }
        public byte ControllerNumber { get; set; }
        public byte MinValue { get; set; }
        public int MaxValue { get; set; } // Changed from byte to int
        public int MidiDeviceIndex { get; set; }
        public string MidiDeviceName { get; set; } = string.Empty;

        public int DisplayChannel => Channel + 1; // User-friendly representation (1-16)

        public string DisplayValue
        {
            get
            {
                return MessageType switch
                {
                    MidiMessageType.Note => NoteNumber.ToString(),
                    MidiMessageType.ControlChange => ControllerNumber.ToString(),
                    MidiMessageType.PitchBend => "N/A",
                    _ => "Unknown"
                };
            }
        }
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