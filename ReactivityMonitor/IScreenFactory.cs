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
    public interface IScreenFactory
    {
        IConnectionScreen CreateConnectionScreen();
        IHomeScreen CreateHomeScreen(IWorkspace workspace);
        IWorkspaceDocumentScreen CreateDocumentScreen(IWorkspaceDocument document);
    }
}
