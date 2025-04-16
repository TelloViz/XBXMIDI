using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XB2Midi.ViewModels
{
    public class BasicMappingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
