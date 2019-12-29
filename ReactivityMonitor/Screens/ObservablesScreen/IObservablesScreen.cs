using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public interface IObservablesScreen : IActivatableViewModel
    {
        IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }
        IObservable<bool> WhenIsUpdatingChanges { get; set; }
    }
}
