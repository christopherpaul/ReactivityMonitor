using Caliburn.Micro;
using System.Collections.ObjectModel;

namespace ReactivityMonitor.Screens.ConnectionScreen
{
    public interface IConnectionScreenViewModel : IScreen
    {
        ReadOnlyObservableCollection<Server> AvailableConnections { get; }
    }
}