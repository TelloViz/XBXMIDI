using System;
using System.Collections.Generic;
using XB2Midi.Models;

namespace XB2Midi.Utilities
{
    public static class EventArgsExtensions
    {
        private static readonly Dictionary<ChordPlaybackEventArgs, int> _deviceIndices = 
            new Dictionary<ChordPlaybackEventArgs, int>();
            
        public static void SetDeviceIndex(this ChordPlaybackEventArgs args, int deviceIndex)
        {
            _deviceIndices[args] = deviceIndex;
        }
        
        public static int GetDeviceIndex(this ChordPlaybackEventArgs args, int defaultIndex = 0)
        {
            return _deviceIndices.TryGetValue(args, out int index) ? index : defaultIndex;
        }
    }
}