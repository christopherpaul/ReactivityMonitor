using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class ObservableItem : ReactiveViewModel
    {
        public ObservableItem(IConcurrencyService concurrencyService)
        {
            var subItems = new ObservableCollection<SubscriptionItem>();
            SubItems = new ReadOnlyObservableCollection<SubscriptionItem>(subItems);

            this.WhenActivated(disposables =>
            {
                subItems.Clear();

                ObservableInstance.Subscriptions
                    .Select(sub => new SubscriptionItem(concurrencyService) { Subscription = sub })
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Subscribe(subItems.Add)
                    .DisposeWith(disposables);
            });
        }

        public IObservableInstance ObservableInstance { get; set; }

        public long SequenceId => ObservableInstance.Created.SequenceId;
        public DateTime Timestamp => ObservableInstance.Created.Timestamp;
        public long ThreadId => ObservableInstance.Created.ThreadId;

        public string MethodName => ObservableInstance.Call.CalledMethod;

        public ReadOnlyObservableCollection<SubscriptionItem> SubItems { get; private set; }
    }
}
