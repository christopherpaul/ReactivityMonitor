using Caliburn.Micro;
using ReactivityMonitor.Model;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens
{
    public interface IWorkspaceDocumentScreen
    {
        string DisplayName { get; }
        IWorkspace Workspace { get; set; }
    }
}
