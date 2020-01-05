using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ReactivityMonitor.Controls
{
    public class SimpleViewport : Decorator
    {
        static SimpleViewport()
        {
            ClipToBoundsProperty.OverrideMetadata(typeof(SimpleViewport), new PropertyMetadata(true));
        }

        public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(double),
            typeof(SimpleViewport),
            new FrameworkPropertyMetadata { AffectsArrange = true });

        public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(double),
            typeof(SimpleViewport),
            new FrameworkPropertyMetadata { AffectsArrange = true });

        public double HorizontalOffset
        {
            get => (double)GetValue(HorizontalOffsetProperty);
            set => SetValue(HorizontalOffsetProperty, value);
        }

        public double VerticalOffset
        {
            get => (double)GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (Child == null)
            {
                return default;
            }

            // As far as child is concerned, it is unconstrained
            Child.Measure(new Size(double.MaxValue, double.MaxValue));
            var childDesiredSize = Child.DesiredSize;

            // Apply the constraint
            return new Size(
                Math.Min(childDesiredSize.Width, constraint.Width),
                Math.Min(childDesiredSize.Height, constraint.Height));
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            if (Child != null)
            {
                var childDesiredSize = Child.DesiredSize;

                // horizontal
                double maxHorizOffset = childDesiredSize.Width - arrangeSize.Width;
                double horizOffset = Math.Max(0, Math.Min(maxHorizOffset, HorizontalOffset));

                // vertical
                double maxVertOffset = childDesiredSize.Height - arrangeSize.Height;
                double vertOffset = Math.Max(0, Math.Min(maxVertOffset, VerticalOffset));

                var childRect = new Rect(new Point(-horizOffset, -vertOffset), childDesiredSize);
                Child.Arrange(childRect);
            }

            return arrangeSize;
        }
    }
}
