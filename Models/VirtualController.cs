using System;

namespace XB2Midi.Models
{
    public class VirtualController : IControllerInputSource
    {
        public event EventHandler<ControllerInputEventArgs>? InputReceived;
        public bool IsConnected => true;

        private bool isInitialized;

        public void Initialize()
        {
            isInitialized = true;
        }

        public void Update()
        {
            // Virtual controller doesn't need polling
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        public void SimulateInput(ControllerInputEventArgs args)
        {
            if (!isInitialized) return;
            InputReceived?.Invoke(this, args);
        }
    }
}