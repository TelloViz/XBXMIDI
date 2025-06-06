using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XB2Midi.Models
{
    /// <summary>
    /// Base class for all mapping managers that provides common functionality
    /// </summary>
    public abstract class MappingManagerBase
    {
        protected List<MidiMapping> mappings = new List<MidiMapping>();
        protected MidiOutput midiOutput;

        public event EventHandler? MappingsChanged;

        public delegate void MappingMessageHandler(string message);
        private MappingMessageHandler? mappingMessageHandler;

        /// <summary>
        /// Constructor requiring a MIDI output device
        /// </summary>
        /// <param name="output">MIDI output device</param>
        public MappingManagerBase(MidiOutput output)
        {
            midiOutput = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// Registers a handler for mapping messages
        /// </summary>
        /// <param name="handler">Message handler</param>
        public void RegisterMappingEventHandler(MappingMessageHandler handler)
        {
            mappingMessageHandler = handler;
        }

        /// <summary>
        /// Triggers a mapping event with a message
        /// </summary>
        /// <param name="message">Message to send</param>
        protected void OnMappingEvent(string message)
        {
            mappingMessageHandler?.Invoke(message);
        }

        /// <summary>
        /// Gets all current mappings
        /// </summary>
        /// <returns>List of mappings</returns>
        public List<MidiMapping> GetCurrentMappings()
        {
            return mappings.ToList();
        }

        /// <summary>
        /// Adds a new mapping
        /// </summary>
        /// <param name="mapping">Mapping to add</param>
        public virtual void AddMapping(MidiMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            mappings.Add(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Removes a mapping
        /// </summary>
        /// <param name="mapping">Mapping to remove</param>
        public virtual void RemoveMapping(MidiMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            mappings.Remove(mapping);
            NotifyMappingsChanged();
        }

        /// <summary>
        /// Gets a mapping for a specific controller input
        /// </summary>
        /// <param name="controllerInput">Controller input to find</param>
        /// <returns>Mapping if found, null otherwise</returns>
        public virtual MidiMapping? GetControllerMapping(string controllerInput)
        {
            if (controllerInput == null)
                return null;

            return mappings.FirstOrDefault(m => 
                string.Equals(m.ControllerInput, controllerInput, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Notifies listeners that mappings have changed
        /// </summary>
        protected void NotifyMappingsChanged()
        {
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves mappings to a file
        /// </summary>
        /// <param name="filePath">Path to save to</param>
        public virtual void SaveMappings(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            // Ensure the file has the correct extension
            string extension = GetFileExtension();
            if (!filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.ChangeExtension(filePath, extension);
            }

            string json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Loads mappings from a file
        /// </summary>
        /// <param name="filePath">Path to load from</param>
        public virtual void LoadMappings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Mapping file not found: {filePath}");

            string json = File.ReadAllText(filePath);
            
            try
            {
                var loadedMappings = JsonSerializer.Deserialize<List<MidiMapping>>(json);
                if (loadedMappings != null)
                {
                    mappings.Clear();
                    mappings.AddRange(loadedMappings);
                    NotifyMappingsChanged();
                }
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error deserializing mappings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles controller input events and sends corresponding MIDI messages
        /// </summary>
        /// <param name="e">Controller input event args</param>
        public abstract void HandleControllerInput(ControllerInputEventArgs e);

        /// <summary>
        /// Gets the file extension for this mapping type
        /// </summary>
        /// <returns>File extension including the dot</returns>
        protected abstract string GetFileExtension();
    }
}