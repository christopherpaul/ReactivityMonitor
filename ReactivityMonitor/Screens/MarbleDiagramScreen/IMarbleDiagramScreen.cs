using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public interface IMarbleDiagramScreen : IActivatableViewModel
    {
        IObservable<IChangeSet<IObservableInstance, long>> ObservableInstances { get; set; }
    }
}
