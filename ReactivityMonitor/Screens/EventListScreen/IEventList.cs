using DynamicData;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public interface IEventList
    {
        IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }
    }
}
