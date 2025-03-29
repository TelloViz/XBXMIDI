using System;

namespace XB2Midi.Models
{
    public interface IControllerInputSource
    {
        event EventHandler<ControllerInputEventArgs> InputReceived;
        bool IsConnected { get; }
        void Initialize();
        void Update();
        void Dispose();
    }
}