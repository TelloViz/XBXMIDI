using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        public Dictionary<string, byte> ButtonChannelMap { get; private set; }
        public Dictionary<string, int> ButtonDeviceMap { get; private set; }

        // Add a dictionary to track active notes per button
        private Dictionary<string, (byte Root, byte Third, byte Fifth, bool IsTriad)> activeNotes = 
            new Dictionary<string, (byte, byte, byte, bool)>();

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

            // Initialize default button channel mappings (all on channel 1)
            ButtonChannelMap = new Dictionary<string, byte>
            {
                { "A", 0 }, // MIDI channels are 0-based internally
                { "B", 0 },
                { "X", 0 },
                { "Y", 0 },
                { "DPadUp", 0 },
                { "DPadRight", 0 },
                { "DPadDown", 0 },
                { "DPadLeft", 0 }
            };

            // Initialize default button device mappings (all on first device)
            ButtonDeviceMap = new Dictionary<string, int>
            {
                { "A", 0 },
                { "B", 0 },
                { "X", 0 },
                { "Y", 0 },
                { "DPadUp", 0 },
                { "DPadRight", 0 },
                { "DPadDown", 0 },
                { "DPadLeft", 0 }
            };
        }

        public void ResetButtonMappings()
        {
            ButtonNoteMap.Clear();
            
            // Restore default mappings
            ButtonNoteMap.Add("A", 60); // C4
            ButtonNoteMap.Add("B", 62); // D4
            ButtonNoteMap.Add("X", 64); // E4
            ButtonNoteMap.Add("Y", 65); // F4
            ButtonNoteMap.Add("DPadUp", 67); // G4
            ButtonNoteMap.Add("DPadRight", 69); // A4
            ButtonNoteMap.Add("DPadDown", 71); // B4
            ButtonNoteMap.Add("DPadLeft", 72); // C5

            // Reset channel mappings
            ButtonChannelMap.Clear();
            ButtonChannelMap.Add("A", 0);
            ButtonChannelMap.Add("B", 0);
            ButtonChannelMap.Add("X", 0);
            ButtonChannelMap.Add("Y", 0);
            ButtonChannelMap.Add("DPadUp", 0);
            ButtonChannelMap.Add("DPadRight", 0);
            ButtonChannelMap.Add("DPadDown", 0);
            ButtonChannelMap.Add("DPadLeft", 0);

            // Reset device mappings
            ButtonDeviceMap.Clear();
            ButtonDeviceMap.Add("A", 0);
            ButtonDeviceMap.Add("B", 0);
            ButtonDeviceMap.Add("X", 0);
            ButtonDeviceMap.Add("Y", 0);
            ButtonDeviceMap.Add("DPadUp", 0);
            ButtonDeviceMap.Add("DPadRight", 0);
            ButtonDeviceMap.Add("DPadDown", 0);
            ButtonDeviceMap.Add("DPadLeft", 0);
            
            Debug.WriteLine("Button mappings reset to defaults");
        }

        public void ClearMappings()
        {
            ButtonNoteMap.Clear();
            ButtonChannelMap.Clear();
            ButtonDeviceMap.Clear();
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
            // Look up note from ButtonNoteMap instead of hardcoding
            if (!ButtonNoteMap.TryGetValue(buttonName, out byte rootNote))
                return false;

            // Adjust for octave setting
            rootNote = (byte)(rootNote + (ChordRootOctave - 4) * 12);

            if (isPressed)
            {
                // Button is being pressed - determine what to play
                byte thirdNote = 0;
                byte fifthNote = 0;
                bool isTriad = false;

                if (leftBumperHeld && !rightBumperHeld)
                {
                    // Minor triad
                    thirdNote = (byte)(rootNote + 3);
                    fifthNote = (byte)(rootNote + 7);
                    isTriad = true;
                }
                else if (!leftBumperHeld && rightBumperHeld)
                {
                    // Major triad
                    thirdNote = (byte)(rootNote + 4);
                    fifthNote = (byte)(rootNote + 7);
                    isTriad = true;
                }
                else if (leftBumperHeld && rightBumperHeld)
                {
                    // Diminished chord
                    thirdNote = (byte)(rootNote + 3);
                    fifthNote = (byte)(rootNote + 6);
                    isTriad = true;
                }
                
                // Store which notes are being played for this button
                activeNotes[buttonName] = (rootNote, thirdNote, fifthNote, isTriad);
                
                // Send the notes
                OnChordRequested(rootNote, thirdNote, fifthNote, true, !isTriad);
            }
            else
            {
                // Button is being released - look up what notes we need to turn off
                if (activeNotes.TryGetValue(buttonName, out var notes))
                {
                    // Turn off the notes that were actually played
                    OnChordRequested(notes.Root, notes.Third, notes.Fifth, false, !notes.IsTriad);
                    
                    // Remove from active notes
                    activeNotes.Remove(buttonName);
                }
            }
            
            return true;
        }

        protected virtual void OnChordRequested(byte rootNote, byte thirdNote,
                                               byte fifthNote, bool isOn, bool playRootOnly = false)
        {
            // Get the button name from the root note
            string buttonName = ButtonNoteMap.FirstOrDefault(x => x.Value == rootNote).Key;
            
            // Default values if button not found
            byte channel = 0;
            int deviceIndex = 0;
            
            // Look up channel and device for this button
            if (!string.IsNullOrEmpty(buttonName))
            {
                if (ButtonChannelMap.TryGetValue(buttonName, out byte ch))
                    channel = ch;
                    
                if (ButtonDeviceMap.TryGetValue(buttonName, out int dev))
                    deviceIndex = dev;
            }
            
            ChordRequested?.Invoke(this, new ChordEventArgs
            {
                RootNote = rootNote,
                ThirdNote = thirdNote,
                FifthNote = fifthNote,
                IsOn = isOn,
                Channel = channel,
                DeviceIndex = deviceIndex,
                ButtonName = buttonName,
                PlayRootOnly = playRootOnly
            });
        }
    }
}