using System;
using System.Windows;
using System.Windows.Data;

namespace XB2Midi.Converters
{

    /// <summary>
    /// Converts a string to a Visibility value based on whether the string contains a specified substring.
    /// </summary>
    /// <remarks>
    /// This converter is used to control the visibility of UI elements based on the presence of a substring in a string value.
    /// If the string contains the specified substring, the element is made visible; otherwise, it is collapsed.
    /// </remarks>
    /// <example>
    /// <code>
    /// <ContentControl Visibility="{Binding Path=SomeStringProperty, Converter={StaticResource StringToVisibilityConverter}, ConverterParameter='VisibleSubstring'}" />
    /// </code>
    /// </example>
    public class StringToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string to a Visibility value based on whether the string contains a specified substring.
        /// </summary>
        /// <param name="value">The string value to check.</param>
        /// <param name="targetType">The target type (not used).</param>
        /// <param name="parameter">The substring to look for.</param>
        /// <param name="culture">The culture info (not used).</param>
        /// <returns>
        /// Returns Visibility.Visible if the string contains the substring; otherwise, returns Visibility.Collapsed.
        /// </returns>
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

        /// <summary>
        /// Converts back from Visibility to string (not implemented).
        /// /// </summary>
        /// <param name="value">The Visibility value to convert back.</param>
        /// <param name="targetType">The target type (not used).</param>
        /// <param name="parameter">The parameter (not used).</param>
        /// <param name="culture">The culture info (not used).</param>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}