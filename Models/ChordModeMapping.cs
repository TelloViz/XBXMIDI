using System.Collections.Generic;

namespace XB2Midi.Models
{
    // Define standard MIDI note numbers as an enum for clarity and consistency
    public enum MidiNotes
    {
        // Octave 3
        C3 = 48,
        CSharp3 = 49,
        D3 = 50,
        DSharp3 = 51,
        E3 = 52,
        F3 = 53,
        FSharp3 = 54,
        G3 = 55,
        GSharp3 = 56,
        A3 = 57,
        ASharp3 = 58,
        B3 = 59,
        
        // Octave 4
        C4 = 60,
        CSharp4 = 61,
        D4 = 62,
        DSharp4 = 63,
        E4 = 64,
        F4 = 65,
        FSharp4 = 66,
        G4 = 67,
        GSharp4 = 68,
        A4 = 69,
        ASharp4 = 70,
        B4 = 71,
        
        // Octave 5
        C5 = 72,
        CSharp5 = 73,
        D5 = 74,
        DSharp5 = 75,
        E5 = 76,
        F5 = 77,
        FSharp5 = 78,
        G5 = 79,
        GSharp5 = 80,
        A5 = 81,
        ASharp5 = 82,
        B5 = 83,
    }
    
    public class ChordModeMapping
    {
        public int ChordRootOctave { get; set; } = 4;
        public byte ChordVelocity { get; set; } = 100;
        public Dictionary<string, byte> ButtonNoteMap { get; set; } = new Dictionary<string, byte>();
        public Dictionary<string, byte> ButtonChannelMap { get; set; } = new Dictionary<string, byte>();
        public Dictionary<string, int> ButtonDeviceMap { get; set; } = new Dictionary<string, int>();
        public bool UseTriggerForVelocity { get; set; } = true; // New property for trigger velocity control
        
        public ChordModeMapping()
        {
            // Default constructor with correct MIDI values
            InitializeDefaultMappings();
        }
        
        private void InitializeDefaultMappings()
        {
            // Set default note mappings using the enum values for clarity
            ButtonNoteMap["A"] = (byte)MidiNotes.C4;       // 60
            ButtonNoteMap["B"] = (byte)MidiNotes.D4;       // 62
            ButtonNoteMap["X"] = (byte)MidiNotes.E4;       // 64
            ButtonNoteMap["Y"] = (byte)MidiNotes.F4;       // 65
            ButtonNoteMap["DPadUp"] = (byte)MidiNotes.G4;  // 67
            ButtonNoteMap["DPadRight"] = (byte)MidiNotes.A4; // 69 (corrected from A5)
            ButtonNoteMap["DPadDown"] = (byte)MidiNotes.B4; // 71 (corrected from B5)
            ButtonNoteMap["DPadLeft"] = (byte)MidiNotes.C5; // 72
        }
        
        public ChordModeMapping(ModeState state)
        {
            // Initialize from current state
            ChordRootOctave = state.ChordRootOctave;
            ChordVelocity = state.ChordVelocity;
            
            // Deep copy the dictionaries
            foreach (var kvp in state.ButtonNoteMap)
                ButtonNoteMap[kvp.Key] = kvp.Value;
                
            foreach (var kvp in state.ButtonChannelMap)
                ButtonChannelMap[kvp.Key] = kvp.Value;
                
            foreach (var kvp in state.ButtonDeviceMap)
                ButtonDeviceMap[kvp.Key] = kvp.Value;
        }
        
        public void ApplyTo(ModeState state)
        {
            // Apply stored settings to state
            state.ChordRootOctave = ChordRootOctave;
            state.ChordVelocity = ChordVelocity;
            
            // Clear existing mappings
            state.ClearMappings();
            
            // Apply our mappings
            foreach (var kvp in ButtonNoteMap)
                state.ButtonNoteMap[kvp.Key] = kvp.Value;
                
            foreach (var kvp in ButtonChannelMap)
                state.ButtonChannelMap[kvp.Key] = kvp.Value;
                
            foreach (var kvp in ButtonDeviceMap)
                state.ButtonDeviceMap[kvp.Key] = kvp.Value;
        }
        
        // Helper method to get note name with correct octave from MIDI note number
        public static string GetNoteName(byte midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }
    }
}
