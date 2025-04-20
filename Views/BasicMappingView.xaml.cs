using System;
using System.Windows;
using System.Windows.Controls;
using XB2Midi.Models;
using XB2Midi.ViewModels;
using XB2Midi.Services; // Add this for IDialogService and DialogService
using NAudio.Midi; // Add this for MidiOutput

namespace XB2Midi.Views
{
    public partial class BasicMappingView : UserControl
    {
        private readonly IDialogService _dialogService;
        public BasicMappingViewModel ViewModel { get; }

        public BasicMappingView()
        {
            InitializeComponent();
            
            // Create dialog service
            _dialogService = new DialogService();

            // Create ViewModel with services
            ViewModel = new BasicMappingViewModel();

            // Set DataContext for binding
            DataContext = ViewModel;

            // Subscribe to ViewModel events
            ViewModel.RequestSaveMappingsFilePath += ViewModel_RequestSaveMappingsFilePath;
            ViewModel.RequestLoadMappingsFilePath += ViewModel_RequestLoadMappingsFilePath;
        }

        // Method to initialize with dependencies
        public void Initialize(MidiOutput output, MappingManager mappingManager)
        {
            ViewModel.Initialize(output, mappingManager);
        }

        // Method to handle controller input
        public void HandleControllerInput(ControllerInputEventArgs e)
        {
            ViewModel.HandleControllerInput(e);
        }

        // Handle file dialog requests from ViewModel
        private void ViewModel_RequestSaveMappingsFilePath(object sender, EventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            string filePath = _dialogService.ShowSaveFileDialog(
                parentWindow,
                "Save Mappings", 
                "JSON files (*.json)|*.json|All files (*.*)|*.*", 
                ".json");
                
            if (filePath != null)
            {
                try
                {
                    ViewModel.SaveMappingsToFile(filePath);
                    _dialogService.ShowMessage(parentWindow, "Mappings saved successfully!", "Success");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError(parentWindow, ex.Message);
                }
            }
        }

        private void ViewModel_RequestLoadMappingsFilePath(object sender, EventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            string filePath = _dialogService.ShowOpenFileDialog(
                parentWindow,
                "Load Mappings", 
                "JSON files (*.json)|*.json|All files (*.*)|*.*", 
                ".json");
                
            if (filePath != null)
            {
                try
                {
                    ViewModel.LoadMappingsFromFile(filePath);
                    _dialogService.ShowMessage(parentWindow, "Mappings loaded successfully!", "Success");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError(parentWindow, ex.Message);
                }
            }
        }

        // Error handler for command execution
        private void Command_Error(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
