using NAudio.Midi;
using System;
using System.Collections.Generic;

namespace XB2Midi.Models
{
    public class MidiOutput : IDisposable
    {
        private Dictionary<int, MidiOut> midiOuts = new();

        public void EnsureDeviceExists(int deviceIndex)
        {
            if (!midiOuts.ContainsKey(deviceIndex))
            {
                midiOuts[deviceIndex] = new MidiOut(deviceIndex);
            }
        }

        public void SendNoteOn(int deviceIndex, byte channel, byte note, byte velocity)
        {
            EnsureDeviceExists(deviceIndex);
            if (midiOuts.TryGetValue(deviceIndex, out var midiOut))
            {
                if (midiOut == null)
                {
                    throw new InvalidOperationException("MIDI output device not initialized");
                }

                // Adjust channel to be 1-based
                byte adjustedChannel = (byte)(channel + 1);
                if (adjustedChannel < 1 || adjustedChannel > 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
                }
                
                // Create and send MIDI message
                int message = (0x90 | channel) | (note << 8) | (velocity << 16);
                midiOut.Send(message);
            }
        }

        public void SendNoteOff(int deviceIndex, byte channel, byte note)
        {
            EnsureDeviceExists(deviceIndex);
            if (midiOuts.TryGetValue(deviceIndex, out var midiOut))
            {
                if (midiOut == null) return;

                // Adjust channel to be 1-based
                byte adjustedChannel = (byte)(channel + 1);
                if (adjustedChannel < 1 || adjustedChannel > 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
                }

                var noteOffEvent = new NoteEvent(0, adjustedChannel, MidiCommandCode.NoteOff, note, 0);
                midiOut.Send(noteOffEvent.GetAsShortMessage());
            }
        }

        public void SendControlChange(int deviceIndex, byte channel, byte controller, byte value)
        {
            EnsureDeviceExists(deviceIndex);
            if (midiOuts.TryGetValue(deviceIndex, out var midiOut))
            {
                if (midiOut == null) return;

                // Adjust channel to be 1-based
                byte adjustedChannel = (byte)(channel + 1);
                if (adjustedChannel < 1 || adjustedChannel > 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
                }

                int message = (value << 16) | (controller << 8) | (0xB0 | ((adjustedChannel - 1) & 0x0F));
                midiOut.Send(message);
            }
        }

        public void SendPitchBend(int deviceIndex, byte channel, int value)
        {
            EnsureDeviceExists(deviceIndex);
            if (midiOuts.TryGetValue(deviceIndex, out var midiOut))
            {
                if (midiOut == null) return;

                // Adjust channel to be 1-based
                byte adjustedChannel = (byte)(channel + 1);
                if (adjustedChannel < 1 || adjustedChannel > 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
                }

                var pitchEvent = new PitchWheelChangeEvent(0, adjustedChannel, value);
                midiOut.Send(pitchEvent.GetAsShortMessage());
            }
        }

        public void Dispose()
        {
            foreach (var midiOut in midiOuts.Values)
            {
                midiOut.Dispose();
            }
            midiOuts.Clear();
        }
    }
}