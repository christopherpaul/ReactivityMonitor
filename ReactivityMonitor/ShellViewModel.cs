using Caliburn.Micro;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Infrastructure;

namespace ReactivityMonitor
{
    public class ShellViewModel : ReactiveConductor<IActivate>, IShell
    {
        public ShellViewModel(IConnectionScreenViewModel connectionScreen)
        {
            WhenActivated(disposables =>
            {
                ActivateItem(connectionScreen);
            });
        }
    }
}