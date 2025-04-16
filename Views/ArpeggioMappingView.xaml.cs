using System.Windows;
using System.Windows.Controls;
using XB2Midi.Models;
using XB2Midi.ViewModels;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for ArpeggioMappingView.xaml
    /// </summary>
    public partial class ArpeggioMappingView : UserControl
    {
        // ViewModel instance - Add private setter to allow reassignment within the class
        public ArpeggioMappingViewModel ViewModel { get; private set; }

        public ArpeggioMappingView()
        {
            InitializeComponent();
            
            // Create the ViewModel
            ViewModel = new ArpeggioMappingViewModel();
            
            // Set DataContext
            this.DataContext = ViewModel;
            
            // Connect the activity log
            ArpeggioActivityLog.ItemsSource = ViewModel.ActivityLog;
        }

        // Method to handle controller input events
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            ViewModel.HandleControllerInput(e);
        }
        
        // Method to update the MIDI output reference
        public void SetMidiOutput(MidiOutput midiOutput)
        {
            // Will be used when we implement actual MIDI functionality
            ViewModel = new ArpeggioMappingViewModel(midiOutput);
            this.DataContext = ViewModel;
            ArpeggioActivityLog.ItemsSource = ViewModel.ActivityLog;
        }
    }
}
