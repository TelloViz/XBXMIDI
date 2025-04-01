using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace XB2Midi.Models
{
    public class ModeState
    {
        // Adjust the order to match the visual tab order: 1, 2, 3, 4
        private readonly ControllerMode[] modeOrder = new[]
        {
            ControllerMode.Basic,    // Tab 1
            ControllerMode.Chord,    // Tab 2
            ControllerMode.Arpeggio, // Tab 3
            ControllerMode.Direct    // Tab 4
        };

        private int currentModeIndex = 0; // Starts with Basic mode (index 0)

        public ControllerMode CurrentMode => modeOrder[currentModeIndex];

        public event EventHandler<ChordEventArgs>? ChordRequested;

        // Chord mode settings
        public int ChordRootOctave { get; set; } = 4;
        public byte ChordVelocity { get; set; } = 100;
        public Dictionary<string, byte> ButtonNoteMap { get; private set; }

        public ModeState()
        {
            Debug.WriteLine($"ModeState initialized with {CurrentMode} mode");

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
                // Both pressed - do nothing or implement special behavior
                return false;
            }
            else if (backPressed)
            {
                // Back button cycles backward through modes
                currentModeIndex = (currentModeIndex - 1 + modeOrder.Length) % modeOrder.Length;
                Debug.WriteLine($"Mode changed to: {CurrentMode} (index {currentModeIndex})");
                return true;
            }
            else if (startPressed)
            {
                // Start button cycles forward through modes
                currentModeIndex = (currentModeIndex + 1) % modeOrder.Length;
                Debug.WriteLine($"Mode changed to: {CurrentMode} (index {currentModeIndex})");
                return true;
            }

            return false;
        }

        public bool ShouldHandleAsMidiControl(string inputName)
        {
            // Ignore mode-cycling buttons
            return inputName != "Back" && inputName != "Start";
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