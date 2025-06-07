using System;
using System.Windows;
using System.Windows.Controls;
using XB2Midi.Models;
using XB2Midi.ViewModels;
using XB2Midi.Services;
using NAudio.Midi;

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

        // Update method to initialize with BasicMappingManager
        public void Initialize(MidiOutput output, BasicMappingManager mappingManager = null)
        {
            ViewModel.Initialize(output, mappingManager);
        }

        // Update file dialog methods to include .basic.json extension
        private void ViewModel_RequestSaveMappingsFilePath(object sender, EventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            string filePath = _dialogService.ShowSaveFileDialog(
                parentWindow,
                "Save Basic Mode Mappings", 
                "Basic Mode Mappings (*.basic.json)|*.basic.json|JSON files (*.json)|*.json|All files (*.*)|*.*", 
                ".basic.json");
                
            if (filePath != null)
            {
                try
                {
                    ViewModel.SaveMappingsToFile(filePath);
                    _dialogService.ShowMessage(parentWindow, "Basic mode mappings saved successfully!", "Success");
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
                "Load Basic Mode Mappings", 
                "Basic Mode Mappings (*.basic.json)|*.basic.json|JSON files (*.json)|*.json|All files (*.*)|*.*", 
                ".basic.json");
                
            if (filePath != null)
            {
                try
                {
                    ViewModel.LoadMappingsFromFile(filePath);
                    _dialogService.ShowMessage(parentWindow, "Basic mode mappings loaded successfully!", "Success");
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
