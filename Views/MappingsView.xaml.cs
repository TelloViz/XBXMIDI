using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows;
using Microsoft.Win32;
using XB2Midi.Models;

namespace XB2Midi.Views
{
    public partial class MappingsView : UserControl
    {
        private MappingManager? mappingManager;

        public ObservableCollection<MappingViewModel> Mappings { get; set; } = new();

        public MappingsView()
        {
            InitializeComponent();
            MappingsListView.ItemsSource = Mappings;
        }

        public void Initialize(MappingManager manager)
        {
            mappingManager = manager;
        }

        private void SaveMappingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Save Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    mappingManager?.SaveMappings(dialog.FileName);
                    MessageBox.Show("Mappings saved successfully!", "Success", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving mappings: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadMappingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Load Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    mappingManager?.LoadMappings(dialog.FileName);
                    UpdateMappings(mappingManager?.GetCurrentMappings() ?? Enumerable.Empty<MidiMapping>());
                    MessageBox.Show("Mappings loaded successfully!", "Success", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading mappings: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void UpdateMappings(IEnumerable<MidiMapping> mappings)
        {
            Mappings.Clear();
            foreach (var mapping in mappings)
            {
                Mappings.Add(new MappingViewModel(mapping));
            }
        }
    }

    public class MappingViewModel
    {
        public string ControllerInput { get; }
        public string MessageType { get; }
        public int Channel { get; }
        public string DisplayNumber { get; }
        public string Status { get; set; } = "Inactive";

        public MappingViewModel(MidiMapping mapping)
        {
            ControllerInput = mapping.ControllerInput;
            MessageType = mapping.MessageType.ToString();
            Channel = mapping.Channel + 1;  // Display 1-based channel numbers
            DisplayNumber = mapping.MessageType switch
            {
                MidiMessageType.Note => mapping.NoteNumber.ToString(),
                MidiMessageType.ControlChange => mapping.ControllerNumber.ToString(),
                _ => "-"
            };
        }
    }
}