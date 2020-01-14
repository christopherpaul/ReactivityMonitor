using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ReactivityMonitor.Infrastructure.Converters
{
    public class VisibilityConverter : IValueConverter
    {
        private static readonly object cUnset = new object();

        public object VisibleWhenEqualTo { get; set; } = cUnset;
        public object CollapsedWhenEqualTo { get; set; } = cUnset;
        public object HiddenWhenEqualTo { get; set; } = cUnset;
        public Visibility Default { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Equals(value, VisibleWhenEqualTo))
            {
                return Visibility.Visible;
            }

            if (Equals(value, CollapsedWhenEqualTo))
            {
                return Visibility.Collapsed;
            }

            if (Equals(value, HiddenWhenEqualTo))
            {
                return Visibility.Hidden;
            }

            return Default;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
