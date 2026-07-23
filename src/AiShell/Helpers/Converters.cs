using System;
using System.Globalization;
using System.Windows.Data;

namespace AJTools.AiShell.Helpers
{
    /// <summary>Used to disable action buttons (IsEnabled) while IsBusy is true.</summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }
    }
}
