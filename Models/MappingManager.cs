using System;
using System.Linq;

namespace XB2Midi.Models
{
    public class MappingManager
    {
        private readonly MidiOutput midiOutput;
        private readonly MappingConfiguration config;

        public MappingManager(MidiOutput midiOutput)
        {
            this.midiOutput = midiOutput;
            this.config = new MappingConfiguration();
        }

        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            var mappings = config.Mappings.Where(m => m.ControllerInput == e.InputName);
            
            foreach (var mapping in mappings)
            {
                // Convert input value to MIDI range
                byte midiValue = ConvertToMidiValue(e.Value, mapping);

                switch (mapping.MessageType)
                {
                    case MidiMessageType.Note:
                        if (mapping.NoteNumber.HasValue)
                        {
                            if (midiValue > 0)
                                midiOutput.SendNoteOn(mapping.NoteNumber.Value, midiValue, mapping.Channel);
                            else
                                midiOutput.SendNoteOff(mapping.NoteNumber.Value, 0, mapping.Channel);
                        }
                        break;

                    case MidiMessageType.ControlChange:
                        if (mapping.ControllerNumber.HasValue)
                        {
                            midiOutput.SendControlChange(mapping.ControllerNumber.Value, midiValue, mapping.Channel);
                        }
                        break;

                    case MidiMessageType.PitchBend:
                        // Convert 0-127 to pitch bend range (-8192 to +8191)
                        int bendValue = (midiValue - 64) * 128;
                        midiOutput.SendPitchBend(bendValue, mapping.Channel);
                        break;
                }
            }
        }

        private byte ConvertToMidiValue(object inputValue, MidiMapping mapping)
        {
            double normalizedValue;

            // Handle different input types
            if (inputValue is byte b)
            {
                normalizedValue = b / 255.0;
            }
            else
            {
                // Handle dynamic object for thumbstick values
                dynamic dyn = inputValue;
                string strValue = dyn.ToString();
                
                if (strValue.Contains("X"))
                {
                    normalizedValue = (Convert.ToDouble(dyn.X) + 32768.0) / 65535.0;
                }
                else if (strValue.Contains("Y"))
                {
                    normalizedValue = (Convert.ToDouble(dyn.Y) + 32768.0) / 65535.0;
                }
                else
                {
                    normalizedValue = Convert.ToDouble(inputValue) / 255.0;
                }
            }

            if (mapping.InvertValue)
                normalizedValue = 1.0 - normalizedValue;

            return (byte)(mapping.MinValue + (normalizedValue * (mapping.MaxValue - mapping.MinValue)));
        }

        public void LoadMappings(string filepath)
        {
            var loaded = MappingConfiguration.LoadFromFile(filepath);
            config.Mappings.Clear();
            config.Mappings.AddRange(loaded.Mappings);
        }

        public void SaveMappings(string filepath)
        {
            config.SaveToFile(filepath);
        }
    }
}