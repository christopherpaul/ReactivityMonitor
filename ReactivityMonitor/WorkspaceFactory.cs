using ReactivityMonitor.Connection;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor
{
    internal sealed class WorkspaceFactory : Factory, IWorkspaceFactory
    {
        public WorkspaceFactory(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public IWorkspace CreateWorkspace(IConnectionModel connectionModel)
        {
            var workspace = GetInstance<IWorkspaceBuilder>();
            workspace.Initialise(connectionModel);

            return workspace;
        }
    }
}
