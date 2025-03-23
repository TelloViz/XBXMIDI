using System;
using System.Windows;
using System.Windows.Media; // Add this for CompositionTarget

using XB2Midi.Models;

namespace XB2Midi.Views
{
    public partial class MainWindow : Window
    {
        private XboxController? controller;
        private MidiOutput? midiOutput;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                controller = new XboxController();
                midiOutput = new MidiOutput();

                controller.InputChanged += Controller_InputChanged;

                // Start polling the controller
                CompositionTarget.Rendering += (s, e) => controller.Update();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}");
                Close();
            }
        }

        private void Controller_InputChanged(object? sender, ControllerInputEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                if (InputLog.Items.Count > 100) InputLog.Items.RemoveAt(InputLog.Items.Count - 1);
            });
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            InputLog.Items.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            controller?.Dispose();
            midiOutput?.Dispose();
            base.OnClosed(e);
        }
    }
}