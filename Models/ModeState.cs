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

        // Update the dictionary to track the seventh note and whether it has a seventh
        private Dictionary<string, (byte Root, byte Third, byte Fifth, byte Seventh, bool IsTriad, bool HasSeventh)> activeNotes = 
            new Dictionary<string, (byte, byte, byte, byte, bool, bool)>();

        // Add state tracking for right bumper double-tap detection
        private DateTime lastRBPress = DateTime.MinValue;
        private bool isRBDoubleTapMode = false;
        private const double DOUBLE_TAP_THRESHOLD_MS = 300; // 300ms threshold for double tap

        // Add tracking for left bumper double-tap 
        private DateTime lastLBPress = DateTime.MinValue;
        private bool isLBDoubleTapMode = false;

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
            // Special handling for right bumper to detect double-taps
            if (buttonName == "RightBumper" && isPressed)
            {
                DateTime now = DateTime.Now;
                double timeSinceLastPress = (now - lastRBPress).TotalMilliseconds;
                
                if (timeSinceLastPress < DOUBLE_TAP_THRESHOLD_MS)
                {
                    // This is a double tap
                    isRBDoubleTapMode = true;
                    Debug.WriteLine("Right bumper double-tap detected - Major 7th mode activated");
                }
                
                lastRBPress = now;
                return false; // Don't process as chord
            }
            
            // Special handling for left bumper to detect double-taps
            if (buttonName == "LeftBumper" && isPressed)
            {
                DateTime now = DateTime.Now;
                double timeSinceLastPress = (now - lastLBPress).TotalMilliseconds;
                
                if (timeSinceLastPress < DOUBLE_TAP_THRESHOLD_MS)
                {
                    // This is a double tap
                    isLBDoubleTapMode = true;
                    Debug.WriteLine("Left bumper double-tap detected - Minor 7th mode activated");
                }
                
                lastLBPress = now;
                return false; // Don't process as chord
            }
            
            // If bumper is released, keep the mode active until a note is played
            if (buttonName == "RightBumper" && !isPressed && isRBDoubleTapMode)
                return false;
                
            if (buttonName == "LeftBumper" && !isPressed && isLBDoubleTapMode)
                return false;

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
                byte seventhNote = 0;
                bool isTriad = false;
                bool hasSeventh = false;

                if (leftBumperHeld && !rightBumperHeld)
                {
                    if (isLBDoubleTapMode)
                    {
                        // Minor 7th chord (1-b3-5-b7)
                        thirdNote = (byte)(rootNote + 3);  // Minor third
                        fifthNote = (byte)(rootNote + 7);  // Perfect fifth
                        seventhNote = (byte)(rootNote + 10); // Minor seventh
                        isTriad = true;
                        hasSeventh = true;
                    }
                    else
                    {
                        // Minor triad
                        thirdNote = (byte)(rootNote + 3);
                        fifthNote = (byte)(rootNote + 7);
                        isTriad = true;
                    }
                }
                else if (!leftBumperHeld && rightBumperHeld)
                {
                    if (isRBDoubleTapMode)
                    {
                        // Major 7th chord (1-3-5-7)
                        thirdNote = (byte)(rootNote + 4);
                        fifthNote = (byte)(rootNote + 7);
                        seventhNote = (byte)(rootNote + 11); // Major 7th
                        isTriad = true;
                        hasSeventh = true;
                    }
                    else
                    {
                        // Major triad
                        thirdNote = (byte)(rootNote + 4);
                        fifthNote = (byte)(rootNote + 7);
                        isTriad = true;
                    }
                }
                else if (leftBumperHeld && rightBumperHeld)
                {
                    // Diminished chord
                    thirdNote = (byte)(rootNote + 3);
                    fifthNote = (byte)(rootNote + 6);
                    isTriad = true;
                }
                
                // Store which notes are being played for this button - include seventh note and hasSeventh flag
                activeNotes[buttonName] = (rootNote, thirdNote, fifthNote, seventhNote, isTriad, hasSeventh);
                
                // Send the notes
                OnChordRequested(rootNote, thirdNote, fifthNote, seventhNote, isOn: true, playRootOnly: !isTriad, hasSeventh: hasSeventh);
                
                // Clear the double-tap states once a non-bumper button is pressed
                if (buttonName != "RightBumper" && buttonName != "LeftBumper")
                {
                    isRBDoubleTapMode = false;
                    isLBDoubleTapMode = false;
                }
            }
            else
            {
                // Button is being released - look up what notes we need to turn off
                if (activeNotes.TryGetValue(buttonName, out var notes))
                {
                    // Turn off the notes that were actually played, using the values stored when pressed
                    OnChordRequested(
                        notes.Root, 
                        notes.Third, 
                        notes.Fifth, 
                        notes.Seventh, 
                        isOn: false, 
                        playRootOnly: !notes.IsTriad, 
                        hasSeventh: notes.HasSeventh
                    );
                    
                    // Remove from active notes
                    activeNotes.Remove(buttonName);
                }
            }
            
            return true;
        }

        protected virtual void OnChordRequested(byte rootNote, byte thirdNote,
                                               byte fifthNote, byte seventhNote,
                                               bool isOn, bool playRootOnly = false,
                                               bool hasSeventh = false)
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
                SeventhNote = seventhNote,
                IsOn = isOn,
                Channel = channel,
                DeviceIndex = deviceIndex,
                ButtonName = buttonName,
                PlayRootOnly = playRootOnly,
                HasSeventh = hasSeventh
            });
        }
    }
}