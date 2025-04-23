using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpectator.Utilities
{
    public class DateTimeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                // For today's dates, show "Today at HH:MM"
                if (dateTime.Date == DateTime.Today)
                {
                    return $"Today at {dateTime:h:mm tt}";
                }
                // For yesterday's dates, show "Yesterday at HH:MM"
                else if (dateTime.Date == DateTime.Today.AddDays(-1))
                {
                    return $"Yesterday at {dateTime:h:mm tt}";
                }
                // For dates within the last week, show the day of week
                else if (dateTime > DateTime.Today.AddDays(-7))
                {
                    return $"{dateTime:dddd} at {dateTime:h:mm tt}";
                }
                // For older dates, show the full date
                else
                {
                    return $"{dateTime:MMM d, yyyy}";
                }
            }

            return "Unknown date";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
