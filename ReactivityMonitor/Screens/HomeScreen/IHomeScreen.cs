using Caliburn.Micro;
using ReactivityMonitor.Workspace;

namespace ReactivityMonitor.Screens.HomeScreen
{
    public interface IHomeScreen : IScreen
    {
        IWorkspace Workspace { get; set; }
    }
}
