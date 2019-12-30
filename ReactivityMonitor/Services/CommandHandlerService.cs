using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Services
{
    public sealed class CommandHandlerService : ICommandHandlerService
    {
        private readonly IConcurrencyService mConcurrencyService;
        private readonly List<(ICommand CommandToHandle, ICommand Handler)> mBindings = new List<(ICommand CommandToHandle, ICommand Handler)>();

        public CommandHandlerService(IConcurrencyService concurrencyService)
        {
            mConcurrencyService = concurrencyService;
        }

        public void HandleCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (TryFindHandler(e.Command, out var handler))
            {
                e.CanExecute = handler.CanExecute(e.Parameter);
                e.Handled = true;
            }
        }

        public void HandleExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (TryFindHandler(e.Command, out var handler))
            {
                handler.Execute(e.Parameter);
                e.Handled = true;
            }
        }

        public IDisposable RegisterHandler(ICommand commandToHandle, ICommand handler)
        {
            mBindings.Add((commandToHandle, handler));
            CommandManager.InvalidateRequerySuggested();
            return StableCompositeDisposable.Create(
                Disposable.Create(() => mBindings.Remove((commandToHandle, handler))),
                Observable.FromEventPattern(h => handler.CanExecuteChanged += h, h => handler.CanExecuteChanged -= h)
                    .ObserveOn(mConcurrencyService.DispatcherRxScheduler)
                    .Subscribe(_ => CommandManager.InvalidateRequerySuggested()));
        }

        private bool TryFindHandler(ICommand command, out ICommand handler)
        {
            foreach (var binding in mBindings)
            {
                if (binding.CommandToHandle == command)
                {
                    handler = binding.Handler;
                    return true;
                }
            }

            handler = null;
            return false;
        }
    }
}
