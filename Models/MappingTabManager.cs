using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XB2Midi.Models
{
    public class MappingTabManager
    {
        private const int MAX_TABS = 4;
        
        public ObservableCollection<ChordModeMapping> ChordMappings { get; private set; }
        public event EventHandler<int> ActiveMappingChanged;
        
        private int activeMappingIndex = 0;
        
        public MappingTabManager()
        {
            ChordMappings = new ObservableCollection<ChordModeMapping>();
            // Always start with at least one default mapping
            ChordMappings.Add(new ChordModeMapping { Name = "Default" });
        }
        
        public int ActiveMappingIndex 
        { 
            get => activeMappingIndex;
            set 
            {
                if (value >= 0 && value < ChordMappings.Count)
                {
                    activeMappingIndex = value;
                    ActiveMappingChanged?.Invoke(this, activeMappingIndex);
                }
            }
        }
        
        public ChordModeMapping? ActiveMapping => 
            activeMappingIndex >= 0 && activeMappingIndex < ChordMappings.Count ? 
            ChordMappings[activeMappingIndex] : null;
            
        public bool CanAddMapping => ChordMappings.Count < MAX_TABS;
        
        public void AddNewMapping()
        {
            if (CanAddMapping)
            {
                // Create a new mapping based on the active one if available
                ChordModeMapping newMapping;
                
                if (ActiveMapping != null)
                {
                    newMapping = ActiveMapping.Clone($"Mapping {ChordMappings.Count + 1}");
                }
                else
                {
                    newMapping = new ChordModeMapping { Name = $"Mapping {ChordMappings.Count + 1}" };
                }
                
                ChordMappings.Add(newMapping);
                
                // Set the new mapping as active
                ActiveMappingIndex = ChordMappings.Count - 1;
            }
        }
        
        public bool RemoveMapping(int index)
        {
            if (index >= 0 && index < ChordMappings.Count && ChordMappings.Count > 1)
            {
                ChordMappings.RemoveAt(index);
                
                // Update active index if needed
                if (activeMappingIndex >= ChordMappings.Count)
                {
                    ActiveMappingIndex = ChordMappings.Count - 1;
                }
                else if (activeMappingIndex == index)
                {
                    // Force event trigger even though index might be the same
                    ActiveMappingChanged?.Invoke(this, activeMappingIndex);
                }
                
                return true;
            }
            
            return false;
        }
        
        public void RenameMappingAt(int index, string newName)
        {
            if (index >= 0 && index < ChordMappings.Count)
            {
                ChordMappings[index].Name = newName;
            }
        }
        
        // Apply a specific mapping to the provided ModeState
        public void ApplyMapping(int mappingIndex, ModeState state)
        {
            if (mappingIndex >= 0 && mappingIndex < ChordMappings.Count)
            {
                ChordMappings[mappingIndex].ApplyTo(state);
            }
        }
        
        // Update a specific mapping from the current ModeState
        public void UpdateMappingFromState(int mappingIndex, ModeState state)
        {
            if (mappingIndex >= 0 && mappingIndex < ChordMappings.Count)
            {
                ChordMappings[mappingIndex] = new ChordModeMapping(state)
                {
                    Name = ChordMappings[mappingIndex].Name
                };
            }
        }
    }
}
