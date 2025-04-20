// Referenced in:
// - ArpeggioMappingViewModel.cs
// - MappingManger.cs
// - MultiMappingViewModel.cs
// - BasicMappingView.xaml.cs
// - ChordMappingView.xaml.cs
// - MainWindow.xaml.cs
// - MultiMappingView.xaml.cs

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
            // Check if we already have this device open
            if (midiOuts.ContainsKey(deviceIndex) && midiOuts[deviceIndex] != null)
                return;
            
            // Check if the requested device is available
            if (!IsDeviceAvailable(deviceIndex))
            {
                // Try to find an alternative available device
                for (int i = 0; i < MidiOut.NumberOfDevices; i++)
                {
                    if (i != deviceIndex && IsDeviceAvailable(i))
                    {
                        System.Diagnostics.Debug.WriteLine($"MIDI device {deviceIndex} unavailable, using device {i} instead");
                        deviceIndex = i;
                        break;
                    }
                }
            }
                
            try
            {
                // Validate device index
                if (deviceIndex < 0 || deviceIndex >= MidiOut.NumberOfDevices)
                {
                    // No devices available - quietly log this instead of throwing
                    System.Diagnostics.Debug.WriteLine($"No MIDI output devices available");
                    return;
                }
                
                // Try to open the MIDI device (may still fail, but less likely after checking)
                midiOuts[deviceIndex] = new MidiOut(deviceIndex);
            }
            catch (NAudio.MmException mmEx)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"MIDI Error: {mmEx.Message} for device {deviceIndex}");
                
                // Don't throw - just log the error and continue
                System.Diagnostics.Debug.WriteLine($"Cannot access MIDI device {deviceIndex}. The device may be in use by another application.");
            }
        }

        // Add this method to check device availability before attempting to use it
        public bool IsDeviceAvailable(int deviceIndex)
        {
            if (deviceIndex < 0 || deviceIndex >= MidiOut.NumberOfDevices)
                return false;
                
            // If we already have it open, it's available
            if (midiOuts.ContainsKey(deviceIndex) && midiOuts[deviceIndex] != null)
                return true;
                
            // Try to open and immediately close the device to test availability
            try
            {
                using (var testDevice = new MidiOut(deviceIndex))
                {
                    // If we get here, the device is available
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public void SendNoteOn(int deviceIndex, byte channel, byte note, byte velocity)
        {
            try
            {
                // Check if device is available first
                if (!IsDeviceAvailable(deviceIndex))
                {
                    System.Diagnostics.Debug.WriteLine($"MIDI device {deviceIndex} is not available, skipping note on message");
                    return; // Skip gracefully instead of throwing an exception
                }
                
                EnsureDeviceExists(deviceIndex);
                if (midiOuts.TryGetValue(deviceIndex, out var midiOut))
                {
                    if (midiOut == null)
                    {
                        // Skip silently as we've already checked availability
                        return;
                    }

                    // Rest of your code remains the same
                    byte adjustedChannel = (byte)(channel + 1);
                    if (adjustedChannel < 1 || adjustedChannel > 16)
                    {
                        throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-15");
                    }
                    
                    int message = (0x90 | channel) | (note << 8) | (velocity << 16);
                    midiOut.Send(message);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the app
                System.Diagnostics.Debug.WriteLine($"Failed to send MIDI Note On: {ex.Message}");
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