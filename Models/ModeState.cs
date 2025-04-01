using System;

namespace XB2Midi.Models
{
    public class ModeState
    {
        public ControllerMode CurrentMode { get; private set; } = ControllerMode.Basic;

        public event EventHandler<ChordEventArgs>? ChordRequested;

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
            if (!isPressed) return false;  // Only handle button press, not release

            byte rootNote;

            // Map face buttons to notes (you can customize these values)
            switch (buttonName)
            {
                case "A": rootNote = 60; break;  // C4
                case "B": rootNote = 62; break;  // D4
                case "X": rootNote = 64; break;  // E4
                case "Y": rootNote = 65; break;  // F4
                case "DPadUp": rootNote = 67; break;  // G4
                case "DPadRight": rootNote = 69; break;  // A4
                case "DPadDown": rootNote = 71; break;  // B4
                case "DPadLeft": rootNote = 72; break;  // C5
                default: return false;  // Not a note button
            }

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