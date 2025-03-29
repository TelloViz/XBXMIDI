using System;

namespace XB2Midi.Models
{
    public interface IControllerInputProvider : IDisposable
    {
        bool IsConnected { get; }
        event EventHandler<ControllerInputEventArgs>? InputChanged;
        void Initialize();
        void Update();
    }
}