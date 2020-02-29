using DynamicData;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Dialogs.QuickEventList
{
    public interface IQuickEventListDialog : IDialogViewModel<Unit>
    {
        string Title { get; set; }
        IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }
        IObservable<ClientEvent> ClientEvents { get; set; }
    }
}
