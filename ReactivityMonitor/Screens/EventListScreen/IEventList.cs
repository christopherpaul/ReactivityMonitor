using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public interface IEventList : IActivatableViewModel
    {
        /// <summary>
        /// Observables to show events from.
        /// </summary>
        IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }

        /// <summary>
        /// Optional. When true, also shows events from observables that feed into those in
        /// <see cref="Observables"/> (transitively).
        /// </summary>
        IObservable<bool> IncludeInputObservables { get; set; }

        /// <summary>
        /// Optional. If set, limits events to the specified range of sequence IDs.
        /// </summary>
        IObservable<(long, long)> SequenceIdRange { get; set; }

        /// <summary>
        /// Optional. Client events to include in the event list.
        /// </summary>
        IObservable<ClientEvent> ClientEvents { get; set; }
    }
}
