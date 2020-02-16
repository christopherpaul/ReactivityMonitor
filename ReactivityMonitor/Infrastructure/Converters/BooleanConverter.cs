using ReactivityMonitor.Utility.Flyweights;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ReactivityMonitor.Infrastructure.Converters
{
    public class BooleanConverter : IValueConverter
    {
        private static readonly object cUnset = new object();

        public object TrueWhenEqualTo { get; set; } = cUnset;
        public object FalseWhenEqualTo { get; set; } = cUnset;
        public bool Default { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Equals(value, TrueWhenEqualTo))
            {
                return Boxes.True;
            }

            if (Equals(value, FalseWhenEqualTo))
            {
                return Boxes.False;
            }

            return Boxes.For(Default);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
