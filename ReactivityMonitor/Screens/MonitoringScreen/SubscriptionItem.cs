using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class SubscriptionItem : ReactiveViewModel
    {
        public SubscriptionItem(IConcurrencyService concurrencyService)
        {
            var streamEvents = new ObservableCollection<StreamEvent>();
            StreamEvents = new ReadOnlyObservableCollection<StreamEvent>(streamEvents);

            this.WhenActivated(observables =>
            {
                streamEvents.Clear();

                Subscription.Events
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Do(e => Trace.WriteLine($"{e}"))
                    .Subscribe(streamEvents.Add)
                    .DisposeWith(observables);
            });
        }

        public ISubscription Subscription { get; set; }

        public ReadOnlyObservableCollection<StreamEvent> StreamEvents { get; private set; }
    }
}
