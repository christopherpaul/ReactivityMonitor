using Caliburn.Micro;
using ReactiveUI;
using ReactivityMonitor.Model;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens
{
    public interface IWorkspaceDocumentScreen : IActivatableViewModel
    {
        string DisplayName { get; }
        IWorkspaceDocument Document { get; }
    }

    public interface IWorkspaceDocumentScreenBuilder<TDocument> : IWorkspaceDocumentScreen
        where TDocument : IWorkspaceDocument
    {
        void SetDocument(TDocument document);
    }
}
