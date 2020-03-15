using ReactivityMonitor.Connection;
using ReactivityMonitor.Screens;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Screens.HomeScreen;
using ReactivityMonitor.Screens.MonitoringScreen;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor
{
    internal sealed class ScreenFactory : Factory, IScreenFactory
    {
        public ScreenFactory(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public IConnectionScreen CreateConnectionScreen() => GetInstance<IConnectionScreen>();

        public IHomeScreen CreateHomeScreen(IWorkspace workspace)
        {
            var homeScreen = GetInstance<IHomeScreen>();
            homeScreen.Workspace = workspace;

            return homeScreen;
        }

        public IMonitoringScreen CreateMonitoringScreen() => GetInstance<IMonitoringScreen>();
    }
}
