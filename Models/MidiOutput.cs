using NAudio.Midi;
using System;
using System.Collections.Generic;

namespace XB2Midi.Models
{
    public class MidiOutput : IDisposable
    {
        private MidiOut? _midiOut;
        private bool disposed;
        private Dictionary<byte, byte> _lastCCValues = new Dictionary<byte, byte>();
        private const int VALUE_CHANGE_THRESHOLD = 10;
        private const int MIN_CC_INTERVAL_MS = 2;
        private DateTime _lastMessageTime;
        private const int MIN_MESSAGE_INTERVAL_MS = 1; // Minimum time between messages
        private const byte MAX_CC_VALUE = 126; // Never send 127 to avoid system resets

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

            try
            {
                // Cap value to prevent system resets
                byte safeValue = Math.Min(value, MAX_CC_VALUE);
                
                // Only send if value changed
                if (!_lastCCValues.ContainsKey(controller) || _lastCCValues[controller] != safeValue)
                {
                    byte statusByte = (byte)(0xB0 | (channel & 0x0F));
                    int midiMessage = (safeValue << 16) | (controller << 8) | statusByte;
                    _midiOut.Send(midiMessage);
                    _lastCCValues[controller] = safeValue;
                }
            }
            catch
            {
                // Ignore errors
            }
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
            if (!disposed)
            {
                _midiOut?.Dispose();
                disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~MidiOutput()
        {
            Dispose();
        }
    }
}