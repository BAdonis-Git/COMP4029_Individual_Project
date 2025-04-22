using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace NeuroSpectator.Utilities
{
    /// <summary>
    /// Converter that returns different colors for stream button based on state
    /// </summary>
    public class StreamingButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isStartingStream && isStartingStream)
            {
                // Return dimmed color when starting
                return Color.FromArgb("#7B61B1"); // Dimmed purple
            }
            else
            {
                // Return normal bright color
                return Color.FromArgb("#B388FF"); // Original purple
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}