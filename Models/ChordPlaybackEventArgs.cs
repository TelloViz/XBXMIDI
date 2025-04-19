using System;
using System.Collections.Generic;

namespace XB2Midi.Models
{
    /// <summary>
    /// Event arguments for chord playback requests
    /// </summary>
    public class ChordPlaybackEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the list of MIDI note numbers in the chord
        /// </summary>
        public List<byte> ChordNotes { get; set; }
        
        /// <summary>
        /// Gets or sets the root note as a MIDI note number
        /// </summary>
        public byte RootNote { get; set; }
        
        /// <summary>
        /// Gets or sets the inversion level (0 = root position, 1 = first inversion, etc.)
        /// </summary>
        public int InversionLevel { get; set; }
    }
}