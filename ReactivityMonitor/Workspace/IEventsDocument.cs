using DynamicData;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Workspace
{
    public interface IEventsDocument : IWorkspaceDocument
    {
        string DocumentName { get; set; }

        /// <summary>
        /// Calls to show events from.
        /// </summary>
        IObservable<IChangeSet<IInstrumentedCall, int>> Calls { get; }

        /// <summary>
        /// Observables to show events from.
        /// </summary>
        IObservable<IChangeSet<IObservableInstance, long>> Observables { get; }
    }
}
