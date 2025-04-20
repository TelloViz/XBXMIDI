using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XB2Midi.Services
{
    /// <summary>
    /// Provides dialog creation and management services for the application.
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// Shows an input dialog to collect text input from the user.
        /// </summary>
        /// <param name="owner">The owner window of the dialog</param>
        /// <param name="title">The title of the dialog</param>
        /// <param name="prompt">The prompt text to display</param>
        /// <param name="defaultValue">The default value for the input field</param>
        /// <param name="result">The result string (output parameter)</param>
        /// <returns>True if the dialog was accepted (OK button), false otherwise</returns>
        public bool ShowInputDialog(Window owner, string title, string prompt, 
                                   string defaultValue, out string result)
        {
            // Create a local variable to store the result
            string inputResult = defaultValue;
            bool dialogResult = false;
            
            // Create dialog window
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            // Create layout grid
            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create prompt label
            var label = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(label, 0);

            // Create input textbox
            var inputBox = new TextBox
            {
                Text = defaultValue,
                MinWidth = 200,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(inputBox, 1);

            // Create button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 2);

            // Create OK button
            var okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                MinWidth = 60,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // Create Cancel button
            var cancelButton = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                MinWidth = 60
            };

            // Add buttons to panel
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            // Add controls to grid
            grid.Children.Add(label);
            grid.Children.Add(inputBox);
            grid.Children.Add(buttonPanel);

            // Set dialog content
            dialog.Content = grid;
            
            // Handle OK button click using a local variable instead of the out parameter
            okButton.Click += (s, e) =>
            {
                inputResult = inputBox.Text;
                dialogResult = true;
                dialog.Close();
            };

            // Show dialog and wait for result
            dialog.ShowDialog();
            
            // Now assign to the out parameter after the lambda has executed
            result = inputResult;
            
            // Return true if dialog was accepted and input is not empty
            return dialogResult && !string.IsNullOrWhiteSpace(result);
        }
        
        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons.
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        /// <param name="isWarning">If true, uses warning icon; otherwise uses question icon</param>
        /// <returns>True if user clicked Yes, false otherwise</returns>
        public bool ShowConfirmationDialog(Window owner, string message, string title, bool isWarning = false)
        {
            MessageBoxImage icon = isWarning ? MessageBoxImage.Warning : MessageBoxImage.Question;
            MessageBoxResult result = MessageBox.Show(
                owner, 
                message,
                title,
                MessageBoxButton.YesNo,
                icon);
                
            return result == MessageBoxResult.Yes;
        }
        
        /// <summary>
        /// Shows an information message to the user.
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <param name="message">The message to display</param>
        /// <param name="title">The dialog title</param>
        public void ShowMessage(Window owner, string message, string title)
        {
            MessageBox.Show(
                owner,
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <param name="message">The error message to display</param>
        /// <param name="title">The dialog title</param>
        public void ShowError(Window owner, string message, string title = "Error")
        {
            MessageBox.Show(
                owner,
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>
        /// Shows a file save dialog to get a file path from the user.
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <param name="title">The dialog title</param>
        /// <param name="filter">The file filter (e.g., "JSON files (*.json)|*.json|All files (*.*)|*.*")</param>
        /// <param name="defaultExt">The default file extension</param>
        /// <returns>The selected file path or null if canceled</returns>
        public string ShowSaveFileDialog(Window owner, string title, string filter, string defaultExt)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                Title = title
            };

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Shows a file open dialog to get a file path from the user.
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <param name="title">The dialog title</param>
        /// <param name="filter">The file filter (e.g., "JSON files (*.json)|*.json|All files (*.*)|*.*")</param>
        /// <param name="defaultExt">The default file extension</param>
        /// <returns>The selected file path or null if canceled</returns>
        public string ShowOpenFileDialog(Window owner, string title, string filter, string defaultExt)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                Title = title
            };

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }
    }
}