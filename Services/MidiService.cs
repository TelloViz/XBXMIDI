using System;
using System.Collections.Generic;
using NAudio.Midi; // Add this for MidiOutput
using XB2Midi.Models;

namespace XB2Midi.Services
{
    public class MidiService : IMidiService
    {
        private readonly MidiOutput _midiOutput;
        
        public MidiService(MidiOutput midiOutput)
        {
            _midiOutput = midiOutput;
        }
        
        public void SendNoteOn(int deviceIndex, byte channel, byte note, byte velocity)
        {
            try
            {
                _midiOutput.SendNoteOn(deviceIndex, channel, note, velocity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MIDI error: {ex.Message}");
                // You could raise an event here to notify the UI
            }
        }
        
        public void SendNoteOff(int deviceIndex, byte channel, byte note)
        {
            _midiOutput.SendNoteOff(deviceIndex, channel, note);
        }
        
        public void PlayChord(List<byte> notes, int deviceIndex, byte channel, byte velocity)
        {
            foreach (byte note in notes)
            {
                SendNoteOn(deviceIndex, channel, note, velocity);
            }
        }
        
        public void ReleaseChord(List<byte> notes, int deviceIndex, byte channel)
        {
            foreach (byte note in notes)
            {
                SendNoteOff(deviceIndex, channel, note);
            }
        }
        
        public int GetNumberOfMidiDevices()
        {
            return MidiOut.NumberOfDevices;
        }
        
        public string GetMidiDeviceName(int deviceIndex)
        {
            return MidiOut.DeviceInfo(deviceIndex).ProductName;
        }
    }
}