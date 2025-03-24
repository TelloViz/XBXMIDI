using System.Collections.ObjectModel;
using System.Windows.Controls;
using XB2Midi.Models;

namespace XB2Midi.Views
{
    public partial class MappingsView : UserControl
    {
        public ObservableCollection<MappingViewModel> Mappings { get; set; } = new();

        public MappingsView()
        {
            InitializeComponent();
            MappingsListView.ItemsSource = Mappings;
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