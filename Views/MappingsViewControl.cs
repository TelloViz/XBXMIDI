using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using XB2Midi.Models;

namespace XB2Midi.Views
{
    public class MappingsViewControl : UserControl
    {
        private ListView? mappingsListView;
        private MappingManager? mappingManager;

        public MappingsViewControl()
        {
            InitializeUI();
        }
        
        // Add this method to match the call in MainWindow.xaml.cs
        public void Initialize(MappingManager manager)
        {
            mappingManager = manager;
            RefreshMappings();
        }
        
        // Add this method to match the call in MainWindow.xaml.cs
        public void UpdateMappings(IEnumerable<MidiMapping> mappings)
        {
            if (mappingsListView == null) return;
            
            // Clear existing items
            mappingsListView.Items.Clear();
            
            // Add each mapping to the ListView
            foreach (var mapping in mappings)
            {
                mappingsListView.Items.Add(new ListViewItem
                {
                    Content = $"{mapping.ControllerInput} → {mapping.MessageType} (Device: {mapping.MidiDeviceName})"
                });
            }
        }
        
        private void InitializeUI()
        {
            // Create the main layout
            Grid mainGrid = new Grid();
            Content = mainGrid;
            
            // Create and configure the ListView
            mappingsListView = new ListView();
            mappingsListView.Margin = new Thickness(5);
            mainGrid.Children.Add(mappingsListView);
        }
        
        public void RefreshMappings()
        {
            // Update the ListView with current mappings
            if (mappingManager != null)
            {
                var mappings = mappingManager.GetCurrentMappings();
                UpdateMappings(mappings);
            }
        }
        
        // Add this method to make the control more useful
        public void ClearMappings()
        {
            if (mappingsListView != null)
            {
                mappingsListView.Items.Clear();
            }
        }
    }
}