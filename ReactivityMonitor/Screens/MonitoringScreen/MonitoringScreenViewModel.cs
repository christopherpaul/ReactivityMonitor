using DynamicData;
using DynamicData.Binding;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ReactivityMonitor.Screens.MonitoringScreen
{
    public sealed class MonitoringScreenViewModel : ReactiveScreen, IMonitoringScreen
    {
        public MonitoringScreenViewModel(IConcurrencyService concurrencyService)
        {
            WhenActivated(disposables =>
            {
                Model.ObservableInstances.Connect()
                    .Transform(obs => new ObservableItem(obs))
                    .Sort(SortExpressionComparer<ObservableItem>.Ascending(obs => obs.SequenceId))
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var observableInstances)
                    .Transform(obsItem => obsItem.SubscribeToSubscriptions(concurrencyService.DispatcherRxScheduler))
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);

                Items = observableInstances;
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<ObservableItem> Items { get; private set; }

        public sealed class ObservableItem
        {
            private readonly IObservableInstance mObs;
            private readonly ObservableCollection<SubscriptionItem> mSubItems;

            public ObservableItem(IObservableInstance obs)
            {
                mObs = obs;
                mSubItems = new ObservableCollection<SubscriptionItem>();
                SubItems = new ReadOnlyObservableCollection<SubscriptionItem>(mSubItems);
            }

            public long SequenceId => mObs.Created.SequenceId;
            public DateTime Timestamp => mObs.Created.Timestamp;
            public long ThreadId => mObs.Created.ThreadId;

            public string MethodName => mObs.Call.CalledMethod;

            public IDisposable SubscribeToSubscriptions(IScheduler dispatcherRxScheduler)
            {
                return mObs.Subscriptions
                    .Select(sub => new SubscriptionItem(sub))
                    .ObserveOn(dispatcherRxScheduler)
                    .Subscribe(Add);
            }

            public ReadOnlyObservableCollection<SubscriptionItem> SubItems { get; }

            private void Add(SubscriptionItem sub) => mSubItems.Add(sub);
        }

        public sealed class SubscriptionItem
        {
            public SubscriptionItem(ISubscription sub)
            {

            }
        }
    }
}
