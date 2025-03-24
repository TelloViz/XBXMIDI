using System;
using System.Windows;
using System.Windows.Data;

namespace XB2Midi.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string? valueString = value.ToString();
            string? paramString = parameter.ToString();

            if (string.IsNullOrEmpty(valueString) || string.IsNullOrEmpty(paramString))
                return Visibility.Collapsed;

            return valueString.Contains(paramString) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}