using System;
using System.Collections;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace NeuroSpectator.Utilities
{
    /// <summary>
    /// Converter to check if a value is zero or empty
    /// </summary>
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
            else if (value is bool boolValue)
            {
                return !boolValue;
            }
            else if (value is string stringValue)
            {
                return string.IsNullOrEmpty(stringValue);
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

    /// <summary>
    /// Converter to convert a percentage string to a double for progress bars
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string percentString && percentString.EndsWith("%"))
            {
                if (double.TryParse(percentString.TrimEnd('%'), out double percent))
                {
                    return percent / 100.0;
                }
            }
            else if (value is double doubleValue)
            {
                return doubleValue / 100.0;
            }
            else if (value is int intValue)
            {
                return intValue / 100.0;
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return $"{doubleValue * 100}%";
            }

            return "0%";
        }
    }

    /// <summary>
    /// Converter to convert wave level text to appropriate colors
    /// </summary>
    public class WaveLevelColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string level)
            {
                return level switch
                {
                    "High" => Color.Parse("#92D36E"),
                    "Medium" => Color.Parse("#FFD740"),
                    "Low" => Color.Parse("#AAAAAA"),
                    _ => Color.Parse("#CCCCCC")
                };
            }

            return Color.Parse("#CCCCCC");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to convert stream health text to appropriate colors
    /// </summary>
    public class StreamHealthColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string health)
            {
                return health switch
                {
                    "Excellent" => Color.Parse("#92D36E"),
                    "Good" => Color.Parse("#92D36E"),
                    "Fair" => Color.Parse("#FFD740"),
                    "Poor" => Color.Parse("#FF5252"),
                    _ => Color.Parse("#CCCCCC")
                };
            }

            return Color.Parse("#CCCCCC");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to toggle between multiple values based on a condition
    /// </summary>
    public class MultiValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string parameters)
            {
                var options = parameters.Split(';');
                if (options.Length >= 2)
                {
                    // If value is true, return first option, otherwise return second option
                    if (value is bool boolValue)
                    {
                        return boolValue ? options[0] : options[1];
                    }
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}