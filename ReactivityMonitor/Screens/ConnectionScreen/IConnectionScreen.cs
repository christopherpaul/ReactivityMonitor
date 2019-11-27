using Caliburn.Micro;
using ReactivityMonitor.Connection;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ReactivityMonitor.Screens.ConnectionScreen
{
    public interface IConnectionScreen : IScreen
    {
        ReadOnlyObservableCollection<Server> AvailableConnections { get; }
        Server SelectedServer { get; set; }
        ICommand OpenSelectedServer { get; }
    }
}