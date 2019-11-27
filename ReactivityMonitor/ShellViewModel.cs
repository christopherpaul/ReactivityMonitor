using System;
using Caliburn.Micro;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Infrastructure;
using ReactiveUI;
using ReactivityMonitor.Services;
using ReactivityMonitor.Screens.HomeScreen;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace ReactivityMonitor
{
    public class ShellViewModel : ReactiveConductor<IActivate>, IShell
    {
        public ShellViewModel(IConnectionService connectionService, IConnectionScreen connectionScreen,
            IHomeScreen homeScreen)
        {
            WhenActivated(disposables =>
            {
                connectionService.WhenConnectionChanges
                    .Where(conn => conn == null)
                    .ObserveOnDispatcher()
                    .Subscribe(_ => ActivateItem(connectionScreen))
                    .DisposeWith(disposables);

                connectionService.WhenConnectionChanges
                    .Where(conn => conn != null)
                    .ObserveOnDispatcher()
                    .Subscribe(conn =>
                    {
                        homeScreen.ConnectionModel = conn;
                        ActivateItem(homeScreen);
                    })
                    .DisposeWith(disposables);
            });
        }
    }
}