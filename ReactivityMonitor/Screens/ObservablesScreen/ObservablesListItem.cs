using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Utility.Extensions;
using static ReactivityMonitor.Model.StreamEvent.EventKind;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public sealed class ObservablesListItem : ReactiveViewModel
    {
        public ObservablesListItem(IConcurrencyService concurrencyService)
        {
            mSubscriptionCount = ObservableAsPropertyHelper<int>.Default();

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                var subCount = ObservableInstance.Subscriptions
                    .Publish(subs => subs
                        .ToObservableChangeSet(sub => sub.SubscriptionId)
                        .Merge(subs.SelectMany(sub => sub.Events
                            .TakeUntil(e => e.Kind == OnCompleted || e.Kind == OnError || e.Kind == Unsubscribe)
                            .WhenTerminated()
                            .Select(_ => new Change<ISubscription, long>(ChangeReason.Remove, sub.SubscriptionId, sub))
                            .Select(chg => new ChangeSet<ISubscription, long> { chg }))))
                    .AsObservableCache()
                    .CountChanged
                    .TakeUntilDisposed(disposables)
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Publish();

                subCount
                    .ToProperty(this, x => x.SubscriptionCount, out mSubscriptionCount)
                    .DisposeWith(disposables);

                subCount.Connect();
            });
        }

        public IObservableInstance ObservableInstance { get; set; }

        public long SequenceId => ObservableInstance.Created.SequenceId;
        public DateTime Timestamp => ObservableInstance.Created.Timestamp;
        public long ThreadId => ObservableInstance.Created.ThreadId;

        private ObservableAsPropertyHelper<int> mSubscriptionCount;
        public int SubscriptionCount => mSubscriptionCount.Value;
    }
}
