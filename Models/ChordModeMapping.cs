using System.Collections.Generic;

namespace XB2Midi.Models
{
    public class ChordModeMapping
    {
        public int ChordRootOctave { get; set; } = 4;
        public byte ChordVelocity { get; set; } = 100;
        public Dictionary<string, byte> ButtonNoteMap { get; set; } = new Dictionary<string, byte>();
        public Dictionary<string, byte> ButtonChannelMap { get; set; } = new Dictionary<string, byte>();
        public Dictionary<string, int> ButtonDeviceMap { get; set; } = new Dictionary<string, int>();
        
        public ChordModeMapping()
        {
            // Default constructor
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
    }
}
