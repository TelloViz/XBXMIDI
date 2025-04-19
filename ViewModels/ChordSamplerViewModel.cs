using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using XB2Midi.Commands;
using XB2Midi.Models;

namespace XB2Midi.ViewModels
{
    public class ChordSamplerViewModel : INotifyPropertyChanged
    {
        #region Properties
        
        private string _selectedRootNote = "C4";
        public string SelectedRootNote
        {
            get => _selectedRootNote;
            set
            {
                if (_selectedRootNote != value)
                {
                    _selectedRootNote = value;
                    OnPropertyChanged();
                    PlayChordCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private int _selectedInversion;
        public int SelectedInversion
        {
            get => _selectedInversion;
            set
            {
                if (_selectedInversion != value)
                {
                    _selectedInversion = value;
                    OnPropertyChanged();
                }
            }
        }

        #region Toggle Properties
        private bool _rootToggleChecked = true;
        public bool RootToggleChecked
        {
            get => _rootToggleChecked;
            set
            {
                if (_rootToggleChecked != value)
                {
                    _rootToggleChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _majThirdToggleChecked;
        public bool MajThirdToggleChecked
        {
            get => _majThirdToggleChecked;
            set
            {
                if (_majThirdToggleChecked != value)
                {
                    _majThirdToggleChecked = value;
                    OnPropertyChanged();
                    if (value && MinThirdToggleChecked)
                    {
                        MinThirdToggleChecked = false;
                    }
                }
            }
        }

        private bool _minThirdToggleChecked;
        public bool MinThirdToggleChecked
        {
            get => _minThirdToggleChecked;
            set
            {
                if (_minThirdToggleChecked != value)
                {
                    _minThirdToggleChecked = value;
                    OnPropertyChanged();
                    if (value && MajThirdToggleChecked)
                    {
                        MajThirdToggleChecked = false;
                    }
                }
            }
        }

        private bool _fifthToggleChecked;
        public bool FifthToggleChecked
        {
            get => _fifthToggleChecked;
            set
            {
                if (_fifthToggleChecked != value)
                {
                    _fifthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && FlatFifthToggleChecked)
                        FlatFifthToggleChecked = false;
                }
            }
        }

        private bool _flatFifthToggleChecked;
        public bool FlatFifthToggleChecked
        {
            get => _flatFifthToggleChecked;
            set
            {
                if (_flatFifthToggleChecked != value)
                {
                    _flatFifthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && FifthToggleChecked)
                        FifthToggleChecked = false;
                }
            }
        }

        private bool _sixthToggleChecked;
        public bool SixthToggleChecked
        {
            get => _sixthToggleChecked;
            set
            {
                if (_sixthToggleChecked != value)
                {
                    _sixthToggleChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _domSeventhToggleChecked;
        public bool DomSeventhToggleChecked
        {
            get => _domSeventhToggleChecked;
            set
            {
                if (_domSeventhToggleChecked != value)
                {
                    _domSeventhToggleChecked = value;
                    OnPropertyChanged();
                    if (value && MajSeventhToggleChecked)
                        MajSeventhToggleChecked = false;
                }
            }
        }

        private bool _majSeventhToggleChecked;
        public bool MajSeventhToggleChecked
        {
            get => _majSeventhToggleChecked;
            set
            {
                if (_majSeventhToggleChecked != value)
                {
                    _majSeventhToggleChecked = value;
                    OnPropertyChanged();
                    if (value && DomSeventhToggleChecked)
                        DomSeventhToggleChecked = false;
                }
            }
        }

        private bool _ninthToggleChecked;
        public bool NinthToggleChecked
        {
            get => _ninthToggleChecked;
            set
            {
                if (_ninthToggleChecked != value)
                {
                    _ninthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && FlatNinthToggleChecked)
                        FlatNinthToggleChecked = false;
                }
            }
        }

        private bool _flatNinthToggleChecked;
        public bool FlatNinthToggleChecked
        {
            get => _flatNinthToggleChecked;
            set
            {
                if (_flatNinthToggleChecked != value)
                {
                    _flatNinthToggleChecked = value;
                    OnPropertyChanged();
                    if (value && NinthToggleChecked)
                        NinthToggleChecked = false;
                }
            }
        }
        #endregion

        #endregion

        #region Commands
        public RelayCommand PlayChordCommand { get; private set; }
        public RelayCommand ClearChordCommand { get; private set; }
        public RelayCommand<string> ChordPresetCommand { get; private set; }
        #endregion

        #region Events
        // Define an event for requesting chord playback
        public event EventHandler<ChordPlaybackEventArgs> ChordPlaybackRequested;
        #endregion

        public ChordSamplerViewModel()
        {
            InitializeCommands();
        }
        
        private void InitializeCommands()
        {
            ClearChordCommand = new RelayCommand(_ => ClearChordToggles());
            ChordPresetCommand = new RelayCommand<string>(ApplyChordPreset);
            PlayChordCommand = new RelayCommand(_ => ExecutePlayChord(), _ => CanExecutePlayChord());
        }

        #region Command Methods
        private void ClearChordToggles()
        {
            RootToggleChecked = true;
            MajThirdToggleChecked = false;
            MinThirdToggleChecked = false;
            FifthToggleChecked = false;
            FlatFifthToggleChecked = false;
            SixthToggleChecked = false;
            DomSeventhToggleChecked = false;
            MajSeventhToggleChecked = false;
            NinthToggleChecked = false;
            FlatNinthToggleChecked = false;
        }

        private void ApplyChordPreset(string presetName)
        {
            // Clear all toggles first
            ClearChordToggles();
            
            // Set root toggle to true
            RootToggleChecked = true;
            
            // Configure chord based on preset
            switch (presetName)
            {
                case "Major":
                    MajThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    break;

                case "Minor":
                    MinThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    break;

                case "Maj7":
                    MajThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    MajSeventhToggleChecked = true;
                    break;

                case "Min7":
                    MinThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    DomSeventhToggleChecked = true;
                    break;

                case "Dom7":
                    MajThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    DomSeventhToggleChecked = true;
                    break;

                case "Dim":
                    MinThirdToggleChecked = true;
                    FlatFifthToggleChecked = true;
                    break;

                case "Sus4":
                    // In Sus4, we omit the third and add a fourth
                    MajThirdToggleChecked = false;
                    MinThirdToggleChecked = false;
                    FifthToggleChecked = true;
                    break;

                case "Add9":
                    MajThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    NinthToggleChecked = true;
                    break;

                case "6":
                    MajThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    SixthToggleChecked = true;
                    break;

                case "m6":
                    MinThirdToggleChecked = true;
                    FifthToggleChecked = true;
                    SixthToggleChecked = true;
                    break;
            }
        }

        private bool CanExecutePlayChord()
        {
            return !string.IsNullOrEmpty(SelectedRootNote);
        }

        private void ExecutePlayChord()
        {
            // Check if root note is selected
            if (string.IsNullOrEmpty(SelectedRootNote))
            {
                return;
            }

            // Create list of notes for the chord
            var chordNotes = DetermineChordNotes();
            
            // Only proceed if we have notes and someone is listening to the event
            if (chordNotes.Count > 0 && ChordPlaybackRequested != null)
            {
                // Notify anyone listening that they should play this chord
                ChordPlaybackRequested?.Invoke(this, new ChordPlaybackEventArgs 
                {
                    ChordNotes = chordNotes,
                    RootNote = GetMidiNoteFromName(SelectedRootNote),
                    InversionLevel = SelectedInversion
                });
            }
        }
        #endregion

        #region Helper Methods
        private List<byte> DetermineChordNotes()
        {
            if (string.IsNullOrEmpty(SelectedRootNote))
                return new List<byte>();

            byte rootNote = GetMidiNoteFromName(SelectedRootNote);
            var notes = new List<byte>();

            if (RootToggleChecked)
                notes.Add(rootNote);

            if (MajThirdToggleChecked)
                notes.Add((byte)(rootNote + 4)); // Major 3rd

            if (MinThirdToggleChecked)
                notes.Add((byte)(rootNote + 3)); // Minor 3rd

            // Special case for Sus4
            if (!MajThirdToggleChecked && !MinThirdToggleChecked)
                if (FifthToggleChecked || FlatFifthToggleChecked)
                    notes.Add((byte)(rootNote + 5)); // Perfect 4th

            if (FifthToggleChecked)
                notes.Add((byte)(rootNote + 7)); // Perfect 5th

            if (FlatFifthToggleChecked)
                notes.Add((byte)(rootNote + 6)); // Diminished 5th

            if (SixthToggleChecked)
                notes.Add((byte)(rootNote + 9)); // Major 6th

            if (DomSeventhToggleChecked)
                notes.Add((byte)(rootNote + 10)); // Dominant 7th

            if (MajSeventhToggleChecked)
                notes.Add((byte)(rootNote + 11)); // Major 7th

            if (NinthToggleChecked)
                notes.Add((byte)(rootNote + 14)); // Major 9th

            if (FlatNinthToggleChecked)
                notes.Add((byte)(rootNote + 13)); // Flat 9th

            return notes;
        }

        private byte GetMidiNoteFromName(string noteText)
        {
            char noteLetter = noteText[0];
            bool isSharp = noteText.Length > 2 && noteText[1] == '#';
            int octave = int.Parse(noteText[noteText.Length - 1].ToString());

            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int noteIndex = Array.FindIndex(noteNames, n => n.StartsWith(noteLetter.ToString()));
            if (isSharp) noteIndex++;

            return (byte)((octave + 1) * 12 + noteIndex);
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }


}