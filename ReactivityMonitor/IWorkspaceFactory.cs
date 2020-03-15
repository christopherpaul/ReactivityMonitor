using ReactivityMonitor.Connection;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor
{
    public interface IWorkspaceFactory
    {
        IWorkspace CreateWorkspace(IConnectionModel connectionModel);
    }
}
