using System;
using System.Collections.Generic;

namespace XB2Midi.Models
{
    public class ModeState
    {
        public ControllerMode CurrentMode { get; private set; } = ControllerMode.Basic;

        public event EventHandler<ChordEventArgs>? ChordRequested;

        // Chord mode settings
        public int ChordRootOctave { get; set; } = 4;
        public byte ChordVelocity { get; set; } = 100;
        public Dictionary<string, byte> ButtonNoteMap { get; private set; }

        public ModeState()
        {
            // Initialize default button note mappings
            ButtonNoteMap = new Dictionary<string, byte>
            {
                { "A", 60 }, // C4
                { "B", 62 }, // D4
                { "X", 64 }, // E4
                { "Y", 65 }, // F4
                { "DPadUp", 67 }, // G4
                { "DPadRight", 69 }, // A4
                { "DPadDown", 71 }, // B4
                { "DPadLeft", 72 } // C5
            };
        }

        public bool HandleModeChange(bool backPressed, bool startPressed)
        {
            if (backPressed && startPressed)
            {
                // Reset to Basic mode
                CurrentMode = ControllerMode.Basic;
                return true;
            }
            else if (backPressed)
            {
                // Cycle backward through modes
                CurrentMode = CurrentMode switch
                {
                    ControllerMode.Basic => ControllerMode.Arpeggio, // Wrap around to the last mode
                    ControllerMode.Direct => ControllerMode.Basic,
                    ControllerMode.Chord => ControllerMode.Direct,
                    ControllerMode.Arpeggio => ControllerMode.Chord,
                    _ => ControllerMode.Basic
                };
                return true;
            }
            else if (startPressed)
            {
                // Cycle forward through modes
                CurrentMode = CurrentMode switch
                {
                    ControllerMode.Basic => ControllerMode.Direct,
                    ControllerMode.Direct => ControllerMode.Chord,
                    ControllerMode.Chord => ControllerMode.Arpeggio,
                    ControllerMode.Arpeggio => ControllerMode.Basic, // Wrap around to the first mode
                    _ => ControllerMode.Basic
                };
                return true;
            }

            return false;
        }

        public bool ShouldHandleAsMidiControl(string inputName)
        {
            // In chord mode, don't handle bumpers as MIDI controls
            if (CurrentMode == ControllerMode.Chord &&
                (inputName == "LeftShoulder" || inputName == "RightShoulder"))
            {
                return false;
            }

            // Don't handle Start and Back as MIDI controls (they're for mode switching)
            return inputName != "Start" && inputName != "Back";
        }

        public bool HandleButtonInput(string buttonName, bool isPressed,
                                     bool leftBumperHeld, bool rightBumperHeld)
        {
            if (!isPressed) return false;

            // Look up note from ButtonNoteMap instead of hardcoding
            if (!ButtonNoteMap.TryGetValue(buttonName, out byte rootNote))
                return false;

            // Adjust for octave setting
            rootNote = (byte)(rootNote + (ChordRootOctave - 4) * 12);

            byte thirdNote, fifthNote;

            if (leftBumperHeld && !rightBumperHeld)
            {
                // Minor chord
                thirdNote = (byte)(rootNote + 3);
                fifthNote = (byte)(rootNote + 7);
            }
            else if (!leftBumperHeld && rightBumperHeld)
            {
                // Dominant 7th
                thirdNote = (byte)(rootNote + 4);
                fifthNote = (byte)(rootNote + 10);
            }
            else if (leftBumperHeld && rightBumperHeld)
            {
                // Diminished
                thirdNote = (byte)(rootNote + 3);
                fifthNote = (byte)(rootNote + 6);
            }
            else
            {
                // Major chord (default)
                thirdNote = (byte)(rootNote + 4);
                fifthNote = (byte)(rootNote + 7);
            }

            OnChordRequested(rootNote, thirdNote, fifthNote, true);
            return true;
        }

        protected virtual void OnChordRequested(byte rootNote, byte thirdNote,
                                               byte fifthNote, bool isOn)
        {
            ChordRequested?.Invoke(this, new ChordEventArgs
            {
                RootNote = rootNote,
                ThirdNote = thirdNote,
                FifthNote = fifthNote,
                IsOn = isOn
            });
        }
    }

    public class ChordEventArgs : EventArgs
    {
        public byte RootNote { get; set; }
        public byte ThirdNote { get; set; }
        public byte FifthNote { get; set; }
        public bool IsOn { get; set; }
    }
}