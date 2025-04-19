using System;
using System.Windows;
using System.Windows.Controls;
using XB2Midi.ViewModels;
using XB2Midi.Models;

namespace XB2Midi.Views
{
    public partial class ChordSamplerView : UserControl
    {
        public ChordSamplerViewModel ViewModel { get; private set; }

        // Create a public event to bubble up chord playback requests
        public event EventHandler<ChordPlaybackEventArgs> ChordPlaybackRequested;

        public ChordSamplerView()
        {
            InitializeComponent();
            
            // Create the view model
            ViewModel = new ChordSamplerViewModel();
            
            // Subscribe to the chord playback requested event from the view model
            ViewModel.ChordPlaybackRequested += OnViewModelChordPlaybackRequested;
            
            // Set as DataContext
            DataContext = ViewModel;
            
            // Populate the combo boxes
            PopulateNoteComboBox();
            PopulateInversionComboBox();
        }

        private void OnViewModelChordPlaybackRequested(object sender, ChordPlaybackEventArgs e)
        {
            // Bubble up the event
            ChordPlaybackRequested?.Invoke(this, e);
        }

        private void PopulateNoteComboBox()
        {
            if (TestChordRootCombo != null)
            {
                TestChordRootCombo.Items.Clear();
                
                // Add octaves 2-6
                for (int octave = 2; octave <= 6; octave++)
                {
                    foreach (string note in new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" })
                    {
                        TestChordRootCombo.Items.Add($"{note}{octave}");
                    }
                }
                
                // Default to C4 (middle C)
                TestChordRootCombo.SelectedItem = "C4";
            }
        }

        private void PopulateInversionComboBox()
        {
            if (ChordInversionCombo != null)
            {
                ChordInversionCombo.Items.Clear();
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "Root Position", Tag = 0 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "1st Inversion", Tag = 1 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "2nd Inversion", Tag = 2 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "3rd Inversion", Tag = 3 });
                ChordInversionCombo.Items.Add(new ComboBoxItem { Content = "4th Inversion", Tag = 4 });
                ChordInversionCombo.SelectedIndex = 0;
            }
        }
    }
}