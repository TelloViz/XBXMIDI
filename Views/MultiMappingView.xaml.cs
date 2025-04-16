using System.Windows;
using System.Windows.Controls;
using XB2Midi.Models;
using XB2Midi.ViewModels;

namespace XB2Midi.Views
{
    /// <summary>
    /// Interaction logic for MultiMappingView.xaml
    /// </summary>
    public partial class MultiMappingView : UserControl
    {
        // ViewModel instance with private setter to allow reassignment within the class
        public MultiMappingViewModel ViewModel { get; private set; }

        public MultiMappingView()
        {
            InitializeComponent();
            
            // Create the ViewModel
            ViewModel = new MultiMappingViewModel();
            
            // Set DataContext
            this.DataContext = ViewModel;
            
            // Connect the activity log
            MultiModeActivityLog.ItemsSource = ViewModel.ActivityLog;
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
            ViewModel = new MultiMappingViewModel(midiOutput);
            this.DataContext = ViewModel;
            MultiModeActivityLog.ItemsSource = ViewModel.ActivityLog;
        }
    }
}
