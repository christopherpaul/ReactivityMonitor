using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class MarbleSubscriptionItem : ReactiveViewModel
    {
        public MarbleSubscriptionItem(IConcurrencyService concurrencyService)
        {
            var streamEvents = new ObservableCollection<StreamEvent>();
            StreamEvents = new ReadOnlyObservableCollection<StreamEvent>(streamEvents);

            this.WhenActivated(observables =>
            {
                streamEvents.Clear();

                Subscription.Events
                    .Gate(WhenIsUpdatingChanges)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Subscribe(streamEvents.Add)
                    .DisposeWith(observables);
            });
        }

        public ISubscription Subscription { get; set; }

        public ReadOnlyObservableCollection<StreamEvent> StreamEvents { get; private set; }

        public IObservable<bool> WhenIsUpdatingChanges { get; set; }
    }
}
