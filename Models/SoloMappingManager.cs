using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XB2Midi.Models
{
    /// <summary>
    /// Manages mappings specifically for Solo Mode
    /// </summary>
    public class SoloMappingManager : MappingManagerBase
    {
        // Track last known trigger values for smooth pitch bend
        private byte lastLeftTriggerValue = 0;
        private byte lastRightTriggerValue = 0;

        // Track last played note for pitch bend reference
        private int currentNoteNumber = -1;
        private bool isNotePlaying = false;

        /// <summary>
        /// Constructor requiring a MIDI output device
        /// </summary>
        /// <param name="output">MIDI output device</param>
        public SoloMappingManager(MidiOutput output) : base(output)
        {
        }

        /// <summary>
        /// Gets the file extension for Solo mode mappings
        /// </summary>
        /// <returns>File extension</returns>
        protected override string GetFileExtension()
        {
            return ".solo.json";
        }

        /// <summary>
        /// Adds a new mapping, ensuring it's set to Solo mode
        /// </summary>
        /// <param name="mapping">Mapping to add</param>
        public override void AddMapping(MidiMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Ensure mapping is for Solo mode
            mapping.Mode = MappingMode.Solo;

            // Remove any existing mapping for this controller input
            mappings.RemoveAll(m => string.Equals(m.ControllerInput, mapping.ControllerInput,
                                                StringComparison.OrdinalIgnoreCase));

            mappings.Add(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Handles controller input events for Solo mode
        /// </summary>
        /// <param name="e">Controller input event args</param>
        public override void HandleControllerInput(ControllerInputEventArgs e)
        {
            var mapping = GetControllerMapping(e.InputName);

            if (mapping != null && midiOutput != null)
            {
                switch (e.InputType)
                {
                    case ControllerInputType.Button:
                        HandleButtonInput(mapping, e);
                        break;

                    case ControllerInputType.Trigger:
                        HandleTriggerInput(mapping, e);
                        break;

                    case ControllerInputType.Thumbstick:
                        HandleThumbstickInput(mapping, e);
                        break;
                }
            }
        }

        private void HandleButtonInput(MidiMapping mapping, ControllerInputEventArgs e)
        {
            bool isPressed = Convert.ToBoolean(e.Value);
            
            switch (mapping.MessageType)
            {
                case MidiMessageType.Note:
                    if (isPressed)
                    {
                        currentNoteNumber = mapping.NoteNumber;
                        isNotePlaying = true;
                        midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber, 127);
                        OnMappingEvent($"Note On: {mapping.NoteNumber} on channel {mapping.Channel + 1}");
                    }
                    else
                    {
                        isNotePlaying = false;
                        midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, mapping.NoteNumber);
                        OnMappingEvent($"Note Off: {mapping.NoteNumber} on channel {mapping.Channel + 1}");
                    }
                    break;
                    
                // Add other button-specific message types here
            }
        }

        private void HandleTriggerInput(MidiMapping mapping, ControllerInputEventArgs e)
        {
            byte triggerValue = Convert.ToByte(e.Value);
            
            // Store trigger values for pitch bend calculation
            if (e.InputName == "LeftTrigger")
                lastLeftTriggerValue = triggerValue;
            else if (e.InputName == "RightTrigger")
                lastRightTriggerValue = triggerValue;

            // Calculate pitch bend based on trigger values
            if (mapping.MessageType == MidiMessageType.PitchBend)
            {
                int pitchBendValue = 8192; // Center position
                
                if (e.InputName == "LeftTrigger")
                    pitchBendValue = 8192 - (triggerValue * 64); // Bend down
                else if (e.InputName == "RightTrigger")
                    pitchBendValue = 8192 + (triggerValue * 64); // Bend up

                pitchBendValue = Math.Clamp(pitchBendValue, 0, 16383);
                
                midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchBendValue);
                OnMappingEvent($"Pitch Bend: {pitchBendValue} on channel {mapping.Channel + 1}");
            }
        }

        private void HandleThumbstickInput(MidiMapping mapping, ControllerInputEventArgs e)
        {
            try
            {
                dynamic stickValue = e.Value;
                float xValue = stickValue.X / 32768.0f; // Normalize to -1 to 1
                float yValue = stickValue.Y / 32768.0f;

                switch (mapping.MessageType)
                {
                    case MidiMessageType.PitchBend:
                        // Use X-axis for pitch bend
                        int pitchBendValue = (int)(8192 + (xValue * 8191));
                        pitchBendValue = Math.Clamp(pitchBendValue, 0, 16383);
                        midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchBendValue);
                        OnMappingEvent($"Pitch Bend: {pitchBendValue} on channel {mapping.Channel + 1}");
                        break;

                    case MidiMessageType.ControlChange:
                        // Use Y-axis for modulation or other CC
                        byte ccValue = (byte)(((yValue + 1.0f) / 2.0f) * 127);
                        midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, 
                                                  mapping.ControllerNumber, ccValue);
                        OnMappingEvent($"CC: {mapping.ControllerNumber} = {ccValue} on channel {mapping.Channel + 1}");
                        break;
                }
            }
            catch (Exception ex)
            {
                OnMappingEvent($"Error handling thumbstick: {ex.Message}");
            }
        }
    }
}
