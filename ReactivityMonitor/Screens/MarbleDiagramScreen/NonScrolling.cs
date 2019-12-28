using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public class NonScrolling : Decorator
    {
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(NonScrolling),
            new FrameworkPropertyMetadata 
            { 
                AffectsMeasure = true,
                AffectsArrange = true,
                DefaultValue = Orientation.Horizontal, 
                PropertyChangedCallback = OnOrientationChanged 
            });

        private static readonly DependencyProperty cScrollOffsetProperty = DependencyProperty.Register(
            nameof(ScrollOffset),
            typeof(double),
            typeof(NonScrolling),
            new FrameworkPropertyMetadata { AffectsArrange = true });

        private static readonly DependencyProperty cViewportSizeProperty = DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(double),
            typeof(NonScrolling),
            new FrameworkPropertyMetadata { AffectsMeasure = true, AffectsArrange = true });

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public double ScrollOffset => (double)GetValue(cScrollOffsetProperty);
        public double ViewportSize => (double)GetValue(cViewportSizeProperty);

        public NonScrolling()
        {
            SetBindings();
        }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NonScrolling)d).SetBindings();
        }

        private void SetBindings()
        {
            BindingOperations.SetBinding(
                this,
                cScrollOffsetProperty,
                new Binding
                {
                    RelativeSource = new RelativeSource
                    {
                        Mode = RelativeSourceMode.FindAncestor,
                        AncestorType = typeof(ScrollViewer)
                    },
                    Path = new PropertyPath(Orientation == Orientation.Horizontal
                        ? ScrollViewer.ContentHorizontalOffsetProperty
                        : ScrollViewer.ContentVerticalOffsetProperty),
                    Mode = BindingMode.OneWay
                });

            BindingOperations.SetBinding(
                this,
                cViewportSizeProperty,
                new Binding
                {
                    RelativeSource = new RelativeSource
                    {
                        Mode = RelativeSourceMode.FindAncestor,
                        AncestorType = typeof(ScrollViewer)
                    },
                    Path = new PropertyPath(Orientation == Orientation.Horizontal
                        ? ScrollViewer.ViewportWidthProperty
                        : ScrollViewer.ViewportHeightProperty),
                    Mode = BindingMode.OneWay
                });
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (Child == null)
            {
                return default;
            }

            Size childConstraint = constraint;
            if (Orientation == Orientation.Horizontal)
            {
                childConstraint.Width = ViewportSize;
            }
            else
            {
                childConstraint.Height = ViewportSize;
            }

            Child.Measure(childConstraint);

            return Child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            if (Child == null)
            {
                return arrangeSize;
            }

            var offset = ScrollOffset;
            var size = ViewportSize;
            if (Orientation == Orientation.Horizontal)
            {
                Child.Arrange(new Rect(
                    offset, 
                    0, 
                    size, 
                    arrangeSize.Height));
            }
            else
            {
                Child.Arrange(new Rect(
                    0,
                    offset,
                    arrangeSize.Width,
                    size));
            }

            return arrangeSize;
        }
    }
}
