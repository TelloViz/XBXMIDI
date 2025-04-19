namespace XB2Midi.Utilities
{
    public static class MusicTheory
    {
        // Move these methods from ChordMappingView.xaml.cs:
        public static string GetNoteName(byte noteNumber)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };   // Array of note names
            int octave = (noteNumber / 12) - 1;                                                         // Calculate the octave based on the note number
            int noteIndex = noteNumber % 12;                                                            // Get the index of the note in the array
            return $"{noteNames[noteIndex]}{octave}";
        }
        public static string DetermineChordName(List<byte> chordNotes, byte rootNote)
        {
            if (chordNotes.Count == 0)  // Check if there are no notes in the chord
                return "(no notes)";    // Return "no notes" message

            bool hasRoot = chordNotes.Contains(rootNote);                // Check if the root note is present in the chord

            if (chordNotes.Count == 1 && !hasRoot)                      // Check if there's only one note and it's not the root
            {
                int interval = chordNotes[0] - rootNote;                // Calculate the interval from the root note
                return $"({GetIntervalName(interval)})";                // Return the interval name in parentheses
            }

            bool hasMinorThird = chordNotes.Contains((byte)(rootNote + 3));         // Check for minor third
            bool hasMajorThird = chordNotes.Contains((byte)(rootNote + 4));         // Check for major third
            bool hasPerfectFourth = chordNotes.Contains((byte)(rootNote + 5));      // Check for perfect fourth
            bool hasDiminishedFifth = chordNotes.Contains((byte)(rootNote + 6));    // Check for diminished fifth
            bool hasPerfectFifth = chordNotes.Contains((byte)(rootNote + 7));       // Check for perfect fifth
            bool hasSixth = chordNotes.Contains((byte)(rootNote + 9));              // Check for sixth
            bool hasDominantSeventh = chordNotes.Contains((byte)(rootNote + 10));   // Check for dominant seventh
            bool hasMajorSeventh = chordNotes.Contains((byte)(rootNote + 11));      // Check for major seventh
            bool hasFlatNinth = chordNotes.Contains((byte)(rootNote + 13));         // Check for flat ninth
            bool hasNinth = chordNotes.Contains((byte)(rootNote + 14));             // Check for ninth

            string quality = ""; // Initialize chord quality string

            if (!hasRoot)   // Check if the root note is not present in the chord
            {
                return "(rootless voicing)";    // Return "rootless voicing" message
            }

            if (!hasMajorThird && !hasMinorThird && hasPerfectFourth) // Check for perfect fourth without major or minor third
            {
                quality = "sus4";   // Sus4 chord
            }
            else if (hasMinorThird && hasDiminishedFifth) // Check for diminished fifth with minor third
            {
                quality = "dim"; // Diminished chord
            }
            else if (hasMinorThird) // Check for minor third without diminished fifth
            {
                quality = "m"; // Minor chord
            }
            else if (hasMajorThird) // Check for major third without minor third
            {
                quality = ""; // Major chord (default)
            }
            else if (!hasMajorThird && !hasMinorThird && !hasPerfectFourth && hasPerfectFifth) // Check for perfect fifth without major or minor third
            {
                quality = "5"; // Power chord (5th)
            }
            else if (chordNotes.Count == 1) // Check if there's only one note in the chord
            {
                return "(root only)"; // Return "root only" message
            }


            if (hasMajorSeventh) // Check for major seventh
            {
                quality += "maj7"; // Major 7th chord
            }
            else if (hasDominantSeventh) // Check for dominant seventh
            {
                quality += "7"; // Dominant 7th chord
            }

            if (hasSixth && !hasMajorSeventh && !hasDominantSeventh) // Check for sixth without major or dominant seventh
            {
                quality += "6"; // Major 6th chord
            }

            if (hasNinth) // Check for ninth
            {
                if (!hasMajorSeventh && !hasDominantSeventh) // Check for ninth without major or dominant seventh
                {
                    quality += "add9"; // Add 9th chord
                }
                else // Check for ninth with major or dominant seventh
                {
                    quality += "9"; // 9th chord
                }
            }
            else if (hasFlatNinth) // Check for flat ninth
            {
                quality += "♭9"; // Flat 9th chord
            }

            return quality; // Return the chord quality string
        }
        public static string GetIntervalName(int semitones)
        {
            return semitones switch // Use switch expression to determine interval name
            {
                0 => "root",
                1 => "minor 2nd",
                2 => "major 2nd",
                3 => "minor 3rd",
                4 => "major 3rd",
                5 => "perfect 4th",
                6 => "diminished 5th",
                7 => "perfect 5th",
                8 => "augmented 5th",
                9 => "major 6th",
                10 => "minor 7th",
                11 => "major 7th",
                12 => "octave",
                13 => "flat 9th",
                14 => "9th",
                _ => $"{semitones} semitones"
            };
        }
        public static List<byte> GetIntervalName(List<byte> chordNotes, int inversionLevel)
        {
            if (inversionLevel == 0 || chordNotes.Count <= 1)                           // No inversion needed for root position or single note
                return new List<byte>(chordNotes);                                      // Return a copy of the original notes

            List<byte> invertedChord = new List<byte>(chordNotes);                      // Create a copy of the original notes
            invertedChord.Sort();                                                       // Sort the notes in ascending order

            for (int i = 0; i < Math.Min(inversionLevel, invertedChord.Count); i++)     // Apply inversion to the lowest notes
            {
                invertedChord[i] = (byte)(invertedChord[i] + 12);                       // Move the note up by an octave
            }

            invertedChord.Sort();                                                       // Sort the inverted chord notes again

            return invertedChord;
        }
        public static byte GetMidiNoteFromName(string noteText)
        {
            char noteLetter = noteText[0];
            bool isSharp = noteText.Length > 2 && noteText[1] == '#';
            int octave = int.Parse(noteText[noteText.Length - 1].ToString());

            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int noteIndex = Array.FindIndex(noteNames, n => n.StartsWith(noteLetter.ToString()));
            if (isSharp) noteIndex++;

            return (byte)((octave + 1) * 12 + noteIndex);
        }
        public static string GetInversionName(int inversion)
        {

            return inversion switch             // Use switch expression to determine inversion name
            {
                1 => "1st inversion",           // 1st inversion
                2 => "2nd inversion",           // 2nd inversion
                3 => "3rd inversion",           // 3rd inversion
                4 => "4th inversion",           // 4th inversion
                _ => "root position"            // Default to root position if no match found
            };
        }
        public static string GetChordType(ChordEventArgs e)
        {
            int third = e.ThirdNote - e.RootNote;
            int fifth = e.FifthNote - e.RootNote;

            if (e.HasNinth)
            {
                int seventh = e.SeventhNote - e.RootNote;
                if (third == 4 && seventh == 11) return "major 9th";
                if (third == 3 && seventh == 10) return "minor 9th";
            }
            else if (e.HasSeventh)
            {
                int seventh = e.SeventhNote - e.RootNote;
                if (third == 4 && seventh == 11) return "major 7th";
                if (third == 3 && seventh == 10) return "minor 7th";
                if (third == 4 && seventh == 10) return "dominant 7th";
            }

            if (third == 4 && fifth == 7) return "major";
            if (third == 3 && fifth == 7) return "minor";
            if (third == 3 && fifth == 6) return "diminished";

            return "custom";
        }
    }
}