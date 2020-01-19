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
using ReactivityMonitor.Screens;

namespace ReactivityMonitor
{
    public class ShellViewModel : ReactiveConductor<IScreen>, IShell
    {
        public ShellViewModel(IConnectionService connectionService, IScreenFactory screenFactory)
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

                Disposable.Create(connectionService.Close).DisposeWith(disposables);
            });

            IScreen GetViewModelForConnection(IConnectionModel conn)
            {
                if (conn == null)
                {
                    return screenFactory.CreateConnectionScreen();
                }
                else
                {
                    var homeScreen = screenFactory.CreateHomeScreen();
                    homeScreen.ConnectionModel = conn;
                    return homeScreen;
                }
            }
        }
    }
}