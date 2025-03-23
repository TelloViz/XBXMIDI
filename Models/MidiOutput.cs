using NAudio.Midi;
using System;

namespace XB2Midi.Models
{
    public class MidiOutput : IDisposable
    {
        private readonly MidiOut midiOut;
        private bool disposed;

        public MidiOutput()
        {
            // Get the first available MIDI output device
            midiOut = new MidiOut(0);
        }

        public void SendNoteOn(byte note, byte velocity, byte channel = 0)
        {
            if (disposed) return;
            int message = (0x90 | channel) | (note << 8) | (velocity << 16);
            midiOut.Send(message);
        }

        public void SendNoteOff(byte note, byte velocity = 0, byte channel = 0)
        {
            if (disposed) return;
            int message = (0x80 | channel) | (note << 8) | (velocity << 16);
            midiOut.Send(message);
        }

        public void SendControlChange(byte controller, byte value, byte channel = 0)
        {
            if (disposed) return;
            int message = (0xB0 | channel) | (controller << 8) | (value << 16);
            midiOut.Send(message);
        }

        public void SendPitchBend(int value, byte channel = 0)
        {
            if (disposed) return;
            // Convert -8192 to +8191 range to 0-16383
            int bendValue = Math.Clamp(value + 8192, 0, 16383);
            byte lsb = (byte)(bendValue & 0x7F);
            byte msb = (byte)((bendValue >> 7) & 0x7F);
            int message = (0xE0 | channel) | (lsb << 8) | (msb << 16);
            midiOut.Send(message);
        }

        public void Dispose()
        {
            disposed = true;
            midiOut?.Dispose();
        }
    }
}