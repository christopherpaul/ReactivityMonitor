using ReactivityMonitor.Screens;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Screens.HomeScreen;
using ReactivityMonitor.Screens.MonitoringScreen;
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

        public IHomeScreen CreateHomeScreen() => GetInstance<IHomeScreen>();

        public IMonitoringScreen CreateMonitoringScreen() => GetInstance<IMonitoringScreen>();
    }
}
