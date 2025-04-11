using System;

namespace XB2Midi.Models
{
    public class ChordEventArgs : EventArgs
    {
        public byte RootNote { get; set; }
        public byte ThirdNote { get; set; }
        public byte FifthNote { get; set; }
        public byte SeventhNote { get; set; }
        public byte NinthNote { get; set; }
        public bool IsOn { get; set; }
        public byte Channel { get; set; }
        public int DeviceIndex { get; set; }
        public string ButtonName { get; set; } = "";
        public bool PlayRootOnly { get; set; }
        public bool HasSeventh { get; set; }
        public bool HasNinth { get; set; }
        public int InversionLevel { get; set; } // Added to store inversion level
    }
}
