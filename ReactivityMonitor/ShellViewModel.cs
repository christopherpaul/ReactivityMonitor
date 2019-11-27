using System;
using Caliburn.Micro;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Services;
using ReactivityMonitor.Screens.HomeScreen;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using ReactivityMonitor.Connection;
using ReactiveUI;
using IScreen = Caliburn.Micro.IScreen;

namespace ReactivityMonitor
{
    public class ShellViewModel : ReactiveConductor<IScreen>, IShell
    {
        public ShellViewModel(IConnectionService connectionService, IConnectionScreen connectionScreen,
            IHomeScreen homeScreen)
        {
            DisplayName = "Reactivity Monitor";

            WhenActivated(disposables =>
            {
                connectionService.WhenConnectionChanges
                    .ObserveOnDispatcher()
                    .Select(GetViewModelForConnection)
                    .Subscribe(ActivateItem)
                    .DisposeWith(disposables);

                this.ObservableForProperty(x => x.ActiveItem).Value()
                    .Select(item => item.WhenAnyValue(x => x.DisplayName))
                    .Switch()
                    .Select(itemTitle => $"{itemTitle} - Reactivity Monitor")
                    .Subscribe(title => DisplayName = title)
                    .DisposeWith(disposables);
            });

            IScreen GetViewModelForConnection(IConnectionModel conn)
            {
                if (conn == null)
                {
                    return connectionScreen;
                }
                else
                {
                    homeScreen.ConnectionModel = conn;
                    return homeScreen;
                }
            }
        }
    }
}