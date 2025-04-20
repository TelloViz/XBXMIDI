using NAudio.Midi;
using System.Collections.Generic;

namespace XB2Midi.Services
{
    public interface IMidiService
    {
        void SendNoteOn(int deviceIndex, byte channel, byte note, byte velocity);
        void SendNoteOff(int deviceIndex, byte channel, byte note);
        void PlayChord(List<byte> notes, int deviceIndex, byte channel, byte velocity);
        void ReleaseChord(List<byte> notes, int deviceIndex, byte channel);
        int GetNumberOfMidiDevices();
        string GetMidiDeviceName(int deviceIndex);
    }
}