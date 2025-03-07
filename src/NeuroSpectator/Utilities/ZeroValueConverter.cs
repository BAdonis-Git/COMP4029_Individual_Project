using System;
using System.Collections;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace NeuroSpectator.Utilities
{
    public class ZeroValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue == 0;
            }
            else if (value is double doubleValue)
            {
                return doubleValue == 0;
            }
            else if (value is ICollection collection)
            {
                return collection.Count == 0;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}