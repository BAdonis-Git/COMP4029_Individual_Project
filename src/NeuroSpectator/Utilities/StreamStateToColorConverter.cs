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
    public class StreamStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StreamStreamerPageModel.StreamStartStateType state)
            {
                // Make sure we're fully qualifying the enum type
                return state switch
                {
                    StreamStreamerPageModel.StreamStartStateType.Ready => Colors.Purple, // Use built-in Colors
                    StreamStreamerPageModel.StreamStartStateType.StartingUp => Colors.BlueViolet,
                    StreamStreamerPageModel.StreamStartStateType.Finalizing => Colors.Blue,
                    StreamStreamerPageModel.StreamStartStateType.Failed => Colors.Red,
                    _ => Colors.Purple
                };
            }
            // Default fallback color
            return Colors.Purple;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
