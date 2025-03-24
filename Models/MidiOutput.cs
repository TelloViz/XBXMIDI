using NAudio.Midi;
using System;

namespace XB2Midi.Models
{
    public class MidiOutput : IDisposable
    {
        private MidiOut? _midiOut;
        private bool disposed;

        public void SetDevice(int deviceIndex)
        {
            _midiOut?.Dispose();
            _midiOut = new MidiOut(deviceIndex);
        }

        public void SendNoteOn(byte channel, byte note, byte velocity)
        {
            if (_midiOut == null)
            {
                throw new InvalidOperationException("MIDI output device not initialized");
            }

            // Adjust channel to be 1-based
            byte adjustedChannel = (byte)(channel + 1);
            if (adjustedChannel < 1 || adjustedChannel > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Sending Note On: Channel={adjustedChannel}, Note={note}, Velocity={velocity}");
                var noteOnEvent = new NoteOnEvent(0, adjustedChannel, note, velocity, 0);
                _midiOut.Send(noteOnEvent.GetAsShortMessage());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending MIDI: {ex.Message}\nStack: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to send MIDI message: {ex.Message}", ex);
            }
        }

        public void SendNoteOff(byte channel, byte note)
        {
            if (_midiOut == null) return;
            
            // Adjust channel to be 1-based
            byte adjustedChannel = (byte)(channel + 1);
            if (adjustedChannel < 1 || adjustedChannel > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
            }
            
            var noteOffEvent = new NoteEvent(0, adjustedChannel, MidiCommandCode.NoteOff, note, 0);
            _midiOut.Send(noteOffEvent.GetAsShortMessage());
        }

        public void SendControlChange(byte channel, byte controller, byte value)
        {
            if (_midiOut == null) return;
            
            // Adjust channel to be 1-based
            byte adjustedChannel = (byte)(channel + 1);
            if (adjustedChannel < 1 || adjustedChannel > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
            }
            
            int message = (value << 16) | (controller << 8) | (0xB0 | ((adjustedChannel - 1) & 0x0F));
            _midiOut.Send(message);
        }

        public void SendPitchBend(byte channel, int value)
        {
            if (_midiOut == null) return;

            // Convert 0-16383 to LSB/MSB bytes
            byte lsb = (byte)(value & 0x7F);        // Lower 7 bits
            byte msb = (byte)((value >> 7) & 0x7F); // Upper 7 bits
            
            // Create pitch bend message (status byte | channel, LSB, MSB)
            int message = ((0xE0 | (channel & 0x0F)) | (lsb << 8) | (msb << 16));
            _midiOut.Send(message);
        }

        public void Dispose()
        {
            _midiOut?.Dispose();
            _midiOut = null;
        }
    }
}