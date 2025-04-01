using System;

namespace XB2Midi.Models
{
    public class ChordEventArgs : EventArgs
    {
        public byte RootNote { get; set; }
        public byte ThirdNote { get; set; }
        public byte FifthNote { get; set; }
        public bool IsOn { get; set; }
        public byte Channel { get; set; } = 0;
        public int DeviceIndex { get; set; } = 0;
        public string ButtonName { get; set; } = string.Empty;
        public bool PlayRootOnly { get; set; } = false;
    }
}
