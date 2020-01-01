using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using ReactivityMonitor.Utility.Extensions;
using System.Diagnostics;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class MarbleObservableItem : ReactiveViewModel
    {
        public MarbleObservableItem(IConcurrencyService concurrencyService)
        {
            var subItems = new ObservableCollection<MarbleSubscriptionItem>();
            SubscriptionItems = new ReadOnlyObservableCollection<MarbleSubscriptionItem>(subItems);

            this.WhenActivated(disposables =>
            {
                subItems.Clear();

                ObservableInstance.Subscriptions
                    .Select(sub => new MarbleSubscriptionItem(concurrencyService) { Subscription = sub, WhenIsUpdatingChanges = WhenIsUpdatingChanges })
                    .Gate(WhenIsUpdatingChanges)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Subscribe(subItems.Add)
                    .DisposeWith(disposables);
            });
        }

        public IObservableInstance ObservableInstance { get; set; }
        public MarbleObservableItem PrimarySink { get; set; }
        public IObservable<bool> WhenIsUpdatingChanges { get; set; }

        private IImmutableList<long> mOrdering;
        public IImmutableList<long> GetOrdering() => LazyInitializer.EnsureInitialized(ref mOrdering, 
            () => (PrimarySink?.GetOrdering() ?? ImmutableList<long>.Empty).Add(ObservableInstance.ObservableId));

        public long SequenceId => ObservableInstance.Created.SequenceId;
        public DateTime Timestamp => ObservableInstance.Created.Timestamp;
        public long ThreadId => ObservableInstance.Created.ThreadId;

        public string MethodName => ObservableInstance.Call.CalledMethod;

        public ReadOnlyObservableCollection<MarbleSubscriptionItem> SubscriptionItems { get; private set; }
    }
}
