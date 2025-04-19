using System;
using System.Windows;
using System.Windows.Controls;
using XB2Midi.Models;
using XB2Midi.ViewModels;

namespace XB2Midi.Views
{
    public partial class BasicMappingView : UserControl
    {
        public BasicMappingViewModel ViewModel { get; }

        public BasicMappingView()
        {
            InitializeComponent();

            // Create ViewModel
            ViewModel = new BasicMappingViewModel();

            // Set DataContext for binding
            DataContext = ViewModel;

            // Subscribe to ViewModel events for file dialogs
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
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Save Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ViewModel.SaveMappingsToFile(dialog.FileName);
                    MessageBox.Show("Mappings saved successfully!", "Success",
MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error",
MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewModel_RequestLoadMappingsFilePath(object sender, EventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                Title = "Load Mappings"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ViewModel.LoadMappingsFromFile(dialog.FileName);
                    MessageBox.Show("Mappings loaded successfully!", "Success",
MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error",
MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Error handler for command execution
        private void Command_Error(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
