using System;
using System.Collections.Generic;

namespace XB2Midi.Utilities
{
    public static class ScaleHelper
    {
        // Major and Minor scale intervals (in semitones)
        private static readonly int[] MajorScaleIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] MinorScaleIntervals = { 0, 2, 3, 5, 7, 8, 10 };

        // Dictionary to map key names to their root MIDI note numbers (relative to C)
        private static readonly Dictionary<string, int> KeyRootNotes = new()
        {
            { "C", 0 },
            { "C#/Db", 1 },
            { "D", 2 },
            { "D#/Eb", 3 },
            { "E", 4 },
            { "F", 5 },
            { "F#/Gb", 6 },
            { "G", 7 },
            { "G#/Ab", 8 },
            { "A", 9 },
            { "A#/Bb", 10 },
            { "B", 11 },
            { "Custom", 0 } // Add Custom key option
        };

        public static List<(int midiNote, string noteName)> GetScaleNotes(string key, string mode, int octave)
        {
            var notes = new List<(int, string)>();
            
            if (key == "Custom")
                return GetAllNotes(octave);

            if (!KeyRootNotes.TryGetValue(key, out int rootNote))
                return notes;

            // Calculate base note for the octave
            int baseNote = (octave + 1) * 12;
            rootNote += baseNote;

            // Select scale pattern based on mode
            var intervals = mode == "Major" ? MajorScaleIntervals : MinorScaleIntervals;

            // Generate scale notes for current octave
            foreach (int interval in intervals)
            {
                int midiNote = rootNote + interval;
                string noteName = GetNoteName(midiNote);
                notes.Add((midiNote, noteName));
            }

            // Add the octave above for additional notes (since we have 8 buttons)
            int nextOctaveRoot = rootNote + 12;
            notes.Add((nextOctaveRoot, GetNoteName(nextOctaveRoot))); // Add the octave note

            return notes;
        }

        public static (string Key, string Mode)? FindMatchingKey(List<int> notes)
        {
            if (notes.Count < 7) return null;

            // Get the intervals between notes relative to the first note
            var intervals = new List<int>();
            int firstNote = notes[0] % 12;
            for (int i = 1; i < notes.Count; i++)
            {
                int interval = (notes[i] % 12 - firstNote + 12) % 12;
                intervals.Add(interval);
            }

            // Check if intervals match major scale pattern
            var majorIntervals = new[] { 0, 2, 4, 5, 7, 9, 11 };
            var minorIntervals = new[] { 0, 2, 3, 5, 7, 8, 10 };

            // Try each possible root note
            foreach (var kvp in KeyRootNotes)
            {
                if (kvp.Key == "Custom") continue;

                // Check if this could be a major key
                if (intervals.SequenceEqual(majorIntervals))
                {
                    return (Key: kvp.Key, Mode: "Major");  // Named tuple syntax
                }

                // Check if this could be a minor key
                if (intervals.SequenceEqual(minorIntervals))
                {
                    return (Key: kvp.Key, Mode: "Minor");  // Named tuple syntax
                }
            }

            return null;
        }

        // Add this helper method to get all notes for an octave
        public static List<(int midiNote, string noteName)> GetAllNotes(int octave)
        {
            var notes = new List<(int midiNote, string noteName)>();
            int baseNote = (octave + 1) * 12;

            for (int i = 0; i < 12; i++)
            {
                int midiNote = baseNote + i;
                string noteName = GetNoteName(midiNote);
                notes.Add((midiNote, noteName));
            }

            return notes;
        }

        public static List<(int midiNote, string noteName)> GetScaleNotesForFullRange(string key, string mode, int octave)
        {
            var notes = new List<(int, string)>();
            
            if (key == "Custom")
                return GetAllNotes(octave);

            // Strip octave number if present in key name
            string keyRoot = key.TrimEnd('2', '3', '4', '5', '6');

            if (!KeyRootNotes.TryGetValue(keyRoot, out int rootNote))
                return notes;

            // Calculate base note for the octave
            int baseNote = (octave + 1) * 12;
            rootNote += baseNote;

            // Select scale pattern
            var intervals = mode == "Major" ? MajorScaleIntervals : MinorScaleIntervals;

            // Generate scale notes
            foreach (int interval in intervals)
            {
                int midiNote = rootNote + interval;
                string noteName = GetNoteName(midiNote);
                notes.Add((midiNote, noteName));
            }

            // Add the octave note
            int nextOctaveRoot = rootNote + 12;
            notes.Add((nextOctaveRoot, GetNoteName(nextOctaveRoot)));

            return notes;
        }

        private static string GetNoteName(int midiNote)
        {
            string[] noteNames = { "C", "C#/Db", "D", "D#/Eb", "E", "F", "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }
    }
}
