using System;
using System.Diagnostics;

namespace XB2Midi.Models
{
    /// <summary>
    /// Manages mappings for Basic Mode
    /// </summary>
    public class BasicMappingManager : MappingManagerBase
    {
        /// <summary>
        /// Constructor requiring a MIDI output device
        /// </summary>
        /// <param name="output">MIDI output device</param>
        public BasicMappingManager(MidiOutput output) : base(output)
        {
        }

        /// <summary>
        /// Gets the file extension for Basic mode mappings
        /// </summary>
        /// <returns>File extension</returns>
        protected override string GetFileExtension()
        {
            return ".basic.json";
        }

        /// <summary>
        /// Adds a new mapping, ensuring it's set to Basic mode
        /// </summary>
        /// <param name="mapping">Mapping to add</param>
        public override void AddMapping(MidiMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            // Ensure mapping is for Basic mode
            mapping.Mode = MappingMode.Basic;
            
            // Remove any existing mapping for this controller input
            mappings.RemoveAll(m => string.Equals(m.ControllerInput, mapping.ControllerInput, 
                                                StringComparison.OrdinalIgnoreCase));
            
            mappings.Add(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Handles controller input events for Basic mode
        /// </summary>
        /// <param name="e">Controller input event args</param>
        public override void HandleControllerInput(ControllerInputEventArgs e)
        {
            var mapping = GetControllerMapping(e.InputName);
            if (mapping == null || midiOutput == null)
                return;

            switch (mapping.MessageType)
            {
                case MidiMessageType.Note:
                    HandleNoteMessage(mapping, e);
                    break;

                case MidiMessageType.ControlChange:
                    HandleControlChangeMessage(mapping, e);
                    break;

                case MidiMessageType.PitchBend:
                    HandlePitchBendMessage(mapping, e);
                    break;
            }
        }

        private void HandleNoteMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            if (e.InputType == ControllerInputType.Button)
            {
                bool isPressed = Convert.ToBoolean(e.Value);
                if (isPressed)
                {
                    midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                        mapping.NoteNumber, 127);
                    OnMappingEvent($"Note On: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
                else
                {
                    midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, 
                                         mapping.NoteNumber);
                    OnMappingEvent($"Note Off: Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                }
            }
            else if (e.InputType == ControllerInputType.Thumbstick)
            {
                try
                {
                    // Map thumbstick value (typically -1.0 to 1.0) to MIDI velocity (0-127)
                    float normalizedValue = (float)e.Value;
                    byte velocity = (byte)Math.Clamp(((normalizedValue + 1.0f) / 2.0f) * 127, 0, 127);
                    
                    midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                        mapping.NoteNumber, velocity);
                    OnMappingEvent($"Note (Continuous): Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}, Velocity {velocity}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling thumbstick note: {ex.Message}");
                }
            }
            else if (e.InputType == ControllerInputType.Trigger)
            {
                // Triggers typically give values from 0.0 to 1.0
                try
                {
                    float normalizedValue = (float)e.Value;
                    byte velocity = (byte)Math.Clamp(normalizedValue * 127, 0, 127);
                    
                    // Only send note on if trigger is pressed enough
                    if (velocity > 0)
                    {
                        midiOutput.SendNoteOn(mapping.MidiDeviceIndex, mapping.Channel, 
                                            mapping.NoteNumber, velocity);
                        OnMappingEvent($"Note (Trigger): Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}, Velocity {velocity}");
                    }
                    else
                    {
                        midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, 
                                             mapping.NoteNumber);
                        OnMappingEvent($"Note Off (Trigger): Channel {mapping.Channel + 1}, Note {mapping.NoteNumber}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling trigger note: {ex.Message}");
                }
            }
        }

        private void HandleControlChangeMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            try
            {
                byte ccValue;
                
                if (e.InputType == ControllerInputType.Button)
                {
                    // Button is either 0 or 1, map to min/max CC value
                    ccValue = Convert.ToBoolean(e.Value) ? (byte)127 : (byte)0;
                }
                else if (e.InputType == ControllerInputType.Thumbstick)
                {
                    // Map thumbstick (-1.0 to 1.0) to CC value (0-127)
                    float normalizedValue = (float)e.Value;
                    ccValue = (byte)Math.Clamp(((normalizedValue + 1.0f) / 2.0f) * 127, 0, 127);
                }
                else if (e.InputType == ControllerInputType.Trigger)
                {
                    // Map trigger (0.0 to 1.0) to CC value (0-127)
                    float normalizedValue = (float)e.Value;
                    ccValue = (byte)Math.Clamp(normalizedValue * 127, 0, 127);
                }
                else
                {
                    // Default case
                    ccValue = 0;
                }
                
                // Change ControlNumber to ControllerNumber
                midiOutput.SendControlChange(mapping.MidiDeviceIndex, mapping.Channel, 
                                      mapping.ControllerNumber, ccValue);
                OnMappingEvent($"CC: Channel {mapping.Channel + 1}, Control {mapping.ControllerNumber}, Value {ccValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling control change: {ex.Message}");
            }
        }

        private void HandlePitchBendMessage(MidiMapping mapping, ControllerInputEventArgs e)
        {
            try
            {
                // Map input value to pitch bend range (0-16383, with 8192 as center)
                int pitchValue;
                
                if (e.InputType == ControllerInputType.Button)
                {
                    // Button is either centered (8192) or max (16383)
                    pitchValue = Convert.ToBoolean(e.Value) ? 16383 : 8192;
                }
                else if (e.InputType == ControllerInputType.Thumbstick)
                {
                    // Map thumbstick (-1.0 to 1.0) to pitch bend (0-16383)
                    float normalizedValue = (float)e.Value;
                    pitchValue = (int)Math.Clamp(((normalizedValue + 1.0f) / 2.0f) * 16383, 0, 16383);
                }
                else if (e.InputType == ControllerInputType.Trigger)
                {
                    // Map trigger (0.0 to 1.0) to upper half of pitch bend (8192-16383)
                    float normalizedValue = (float)e.Value;
                    pitchValue = (int)Math.Clamp(8192 + (normalizedValue * 8191), 8192, 16383);
                }
                else
                {
                    // Default to center
                    pitchValue = 8192;
                }
                
                midiOutput.SendPitchBend(mapping.MidiDeviceIndex, mapping.Channel, pitchValue);
                OnMappingEvent($"Pitch Bend: Channel {mapping.Channel + 1}, Value {pitchValue}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling pitch bend: {ex.Message}");
            }
        }
    }
}
