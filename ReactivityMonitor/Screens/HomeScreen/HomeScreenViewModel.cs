using ReactivityMonitor.Connection;
using ReactivityMonitor.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.HomeScreen
{
    public sealed class HomeScreenViewModel : ReactiveScreen, IHomeScreen
    {
        public HomeScreenViewModel()
        {
            WhenActivated(disposables =>
            {
                ConnectionModel.Connect().DisposeWith(disposables);
            });
        }

        public IConnectionModel ConnectionModel { get; set; }
    }
}
