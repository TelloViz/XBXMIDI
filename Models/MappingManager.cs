using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    public class MappingManager
    {
        private List<MidiMapping> mappings = new List<MidiMapping>();
        private MidiOutput midiOutput;
        private Dictionary<MappingMode, object> modeMappings = new Dictionary<MappingMode, object>();

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

        public MidiMapping? GetControllerMapping(string inputName)
        {
            return mappings.FirstOrDefault(m => m.ControllerInput == inputName);
        }

        public void SaveMappings(string filePath)
        {
            var fileData = new MappingFileData
            {
                BasicMappings = mappings.Where(m => m.Mode == MappingMode.Basic).ToList(),
                ChordMapping = modeMappings.ContainsKey(MappingMode.Chord) ?
                    (ChordModeMapping)modeMappings[MappingMode.Chord] : null,
                // Add other mode mappings as needed
            };

            string jsonString = JsonSerializer.Serialize(fileData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, jsonString);
        }

        public void LoadMappings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Mapping file not found", filePath);

            string jsonString = File.ReadAllText(filePath);
            var fileData = JsonSerializer.Deserialize<MappingFileData>(jsonString);

            if (fileData == null)
                throw new InvalidOperationException("Invalid mapping file format");

            // Load basic mappings
            if (fileData.BasicMappings != null)
            {
                mappings = fileData.BasicMappings;
                MappingsChanged?.Invoke(this, EventArgs.Empty);
            }

            // Load chord mappings
            if (fileData.ChordMapping != null)
            {
                modeMappings[MappingMode.Chord] = fileData.ChordMapping;
                ChordMappingsLoaded?.Invoke(this, fileData.ChordMapping);
            }

            // Add other mode mappings as needed
        }

        public void SaveChordMapping(ModeState state)
        {
            var chordMapping = new ChordModeMapping(state);
            modeMappings[MappingMode.Chord] = chordMapping;
        }

        public bool LoadChordMapping(ModeState state)
        {
            if (modeMappings.ContainsKey(MappingMode.Chord) &&
                modeMappings[MappingMode.Chord] is ChordModeMapping chordMapping)
            {
                chordMapping.ApplyTo(state);
                return true;
            }
            return false;
        }

        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            var mapping = GetControllerMapping(e.InputName);
            if (mapping != null)
            {
                // Handle the mapping...
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