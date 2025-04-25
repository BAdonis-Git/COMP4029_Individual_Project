using NeuroSpectator.PageModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NeuroSpectator.PageModels.StreamStreamerPageModel;

namespace NeuroSpectator.Utilities
{
    public class IsReadyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StreamStreamerPageModel.StreamStartStateType state)
            {
                return state == StreamStreamerPageModel.StreamStartStateType.Ready;
            }
            return true; // Default to enabled if unknown state
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
