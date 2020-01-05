using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public static class ScrollCoordinator
    {
        public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.RegisterAttached(
            "HorizontalOffset",
            typeof(double),
            typeof(ScrollCoordinator),
            new FrameworkPropertyMetadata(HandleHorizontalOffsetPropertyChanged));

        public static double GetHorizontalOffset(ScrollViewer scrollViewer) => (double)scrollViewer.GetValue(HorizontalOffsetProperty);
        public static void SetHorizontalOffset(ScrollViewer scrollViewer, double value) => scrollViewer.SetValue(HorizontalOffsetProperty, value);

        private static void HandleHorizontalOffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ScrollViewer scrollViewer))
            {
                return;
            }

            scrollViewer.ScrollToHorizontalOffset((double)e.NewValue);
        }
    }
}
