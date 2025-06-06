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
                }
                else
                {
                    midiOutput.SendNoteOff(mapping.MidiDeviceIndex, mapping.Channel, 
                                         mapping.NoteNumber);
                }
            }
            else if (e.InputType == ControllerInputType.Thumbstick)
            {
                try
                {
                