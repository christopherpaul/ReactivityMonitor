using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReactivityMonitor.Infrastructure
{
    public class Activation : FrameworkElement, IActivatableView, IViewFor
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty
            .Register(nameof(ViewModel), typeof(object), typeof(Activation));

        private static readonly IEnumerable<IDisposable> cNoDisposables = Enumerable.Empty<IDisposable>();
        private static readonly Func<IEnumerable<IDisposable>> cEmptyBlock = () => cNoDisposables;

        public Activation()
        {
            this.WhenActivated(cEmptyBlock);
        }

        public object ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
    }
}
