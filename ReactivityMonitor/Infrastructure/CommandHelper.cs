using ReactiveUI;
using ReactivityMonitor.Utility;
using ReactivityMonitor.Utility.Flyweights;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Utility.Extensions;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows;

namespace ReactivityMonitor.Infrastructure
{
    public static class CommandHelper
    {
        /// <summary>
        /// Creates a command that pushes a Unit value through the returned observable when
        /// it is executed. The command's CanExecute property is true when the returned
        /// observable is subscribed to.
        /// </summary>
        /// <param name="command">The created command.</param>
        /// <returns>An observable that produces a value when the command is executed.</returns>
        public static IObservable<Unit> CreateTriggerCommand(out ICommand command)
        {
            var canExecute = new ObservablePromise<bool>();
            var commandImpl = ReactiveCommand.Create(Actions.NoOp, canExecute);
            var observable = commandImpl.MonitorSubscriptionCount(out IObservable<int> subCount);
            canExecute.Resolve(subCount.Select(c => c > 0));
            command = commandImpl;
            return observable;
        }

        public static CommandBinding CreateBinding(ICommand routedCommand, ICommand handlerCommand)
        {
            return new CommandBindingToCommand(routedCommand, handlerCommand);
        }

        public static DependencyProperty CommandBindingsProperty = DependencyProperty.RegisterAttached("CommandBindings", typeof(CommandBindingCollection), typeof(CommandHelper),
            new FrameworkPropertyMetadata(OnCommandBindingsChanged));

        public static CommandBindingCollection GetCommandBindings(FrameworkElement fe) => (CommandBindingCollection)fe.GetValue(CommandBindingsProperty);
        public static void SetCommandBindings(FrameworkElement fe, CommandBindingCollection value) => fe.SetValue(CommandBindingsProperty, value);

        private static void OnCommandBindingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                var oldBindings = (e.OldValue as CommandBindingCollection)?.Cast<CommandBinding>() ?? Enumerable.Empty<CommandBinding>();
                var newBindings = (e.NewValue as CommandBindingCollection)?.Cast<CommandBinding>() ?? Enumerable.Empty<CommandBinding>();

                var addedBindings = newBindings.Except(oldBindings);
                var removedBindings = oldBindings.Except(newBindings);

                foreach (var b in removedBindings)
                {
                    fe.CommandBindings.Remove(b);

                    if (b is CommandBindingToCommand cb2c)
                    {
                        cb2c.HandlerCommand.CanExecuteChanged -= cSuggestCanExecuteRequery;
                    }
                }

                foreach (var b in addedBindings)
                {
                    fe.CommandBindings.Add(b);

                    if (b is CommandBindingToCommand cb2c)
                    {
                        cb2c.HandlerCommand.CanExecuteChanged += cSuggestCanExecuteRequery;
                    }
                }
            }
        }

        private class CommandBindingToCommand : CommandBinding
        {
            public CommandBindingToCommand(ICommand routedCommand, ICommand handlerCommand) :
                base(routedCommand, (sender, e) => handlerCommand.Execute(e.Parameter), (sender, e) => e.CanExecute = handlerCommand.CanExecute(e.Parameter))
            {
                HandlerCommand = handlerCommand;
            }

            public ICommand HandlerCommand { get; }
        }

        private static readonly EventHandler cSuggestCanExecuteRequery = (sender, e) => CommandManager.InvalidateRequerySuggested();
    }
}
