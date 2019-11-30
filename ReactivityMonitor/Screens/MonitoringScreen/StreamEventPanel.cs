using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public class StreamEventPanel : Panel
    {
        public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register(
            nameof(StartTime),
            typeof(DateTime?),
            typeof(StreamEventPanel),
            new FrameworkPropertyMetadata { AffectsArrange = true });

        public static readonly DependencyProperty TimeScaleProperty = DependencyProperty.Register(
            nameof(TimeScale), 
            typeof(double), 
            typeof(StreamEventPanel),
            new FrameworkPropertyMetadata((double)10, FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// Horizontal scale, in pixels per second.
        /// </summary>
        public double TimeScale
        {
            get => (double)GetValue(TimeScaleProperty);
            set => SetValue(TimeScaleProperty, value);
        }

        /// <summary>
        /// Time corresponding to the left edge of the panel (defaults to timestamp of first child).
        /// </summary>
        public DateTime? StartTime
        {
            get => (DateTime?)GetValue(StartTimeProperty);
            set => SetValue(StartTimeProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size noConstraint = new Size(Double.PositiveInfinity, Double.PositiveInfinity);
            Size desiredSize = default;

            foreach (UIElement child in InternalChildren)
            {
                if (child == null)
                {
                    continue;
                }

                child.Measure(noConstraint);
                desiredSize.Height = Math.Max(desiredSize.Height, child.DesiredSize.Height);
            }

            return desiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double middle = finalSize.Height / 2;
            DateTime? startTime = StartTime;
            double timeScale = TimeScale;

            foreach (UIElement child in InternalChildren)
            {
                if (child == null)
                {
                    continue;
                }

                Size childSize = child.DesiredSize;
                double top = middle - childSize.Height / 2;

                DateTime childTimestamp = GetChildTimestamp(child);
                if (!startTime.HasValue)
                {
                    startTime = childTimestamp;
                }

                double left = (childTimestamp - startTime.Value).TotalSeconds * timeScale - childSize.Width / 2;

                child.Arrange(new Rect(left, top, childSize.Width, childSize.Height));
            }

            return finalSize;
        }

        private DateTime GetChildTimestamp(UIElement child)
        {
            if (child is FrameworkElement fe && fe.DataContext is StreamEvent se)
            {
                return se.Info.Timestamp;
            }

            return default;
        }
    }
}
