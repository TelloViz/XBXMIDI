using System;
using System.Windows;

namespace XB2Midi.Services
{
    public interface IDialogService
    {
        bool ShowInputDialog(Window owner, string title, string prompt, string defaultValue, out string result);
        bool ShowConfirmationDialog(Window owner, string message, string title, bool isWarning = false);
        void ShowMessage(Window owner, string message, string title);
        void ShowError(Window owner, string message, string title = "Error");
        
        // New file dialog methods
        string ShowSaveFileDialog(Window owner, string title, string filter, string defaultExt);
        string ShowOpenFileDialog(Window owner, string title, string filter, string defaultExt);
    }
}