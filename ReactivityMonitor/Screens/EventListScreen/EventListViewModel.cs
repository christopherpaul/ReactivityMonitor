using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public sealed class EventListViewModel : ReactiveViewModel, IEventList
    {
        public EventListViewModel(IConcurrencyService concurrencyService)
        {
            var eventsCollection = new ObservableCollectionExtended<EventItem>();
            Events = new ReadOnlyObservableCollection<EventItem>(eventsCollection);

            this.WhenActivated((CompositeDisposable disposables) => 
            {
                var observablesWhileNoRemovals = Observables
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .TakeWhile(change => change.Removes == 0)
                    .Transform(obs => Observable.Return(obs))
                    .MergeMany(obs =>
                        IncludeInputObservables.Select(includeInputs => includeInputs
                            ? obs.Expand(obs => obs.Inputs)
                            : obs).Switch())
                    .Distinct(obs => obs.ObservableId);

                var eventsWhileNoRemovals = observablesWhileNoRemovals
                    .SelectMany(obs => Observable.Return(EventItem.FromObservableInstance(obs))
                        .Concat(obs.Subscriptions.SelectMany(sub => sub.Events.Select(e => EventItem.FromStreamEvent(sub, e)))))
                    .Merge(ClientEvents?.Select(EventItem.FromClientEvent) ?? Observable.Empty<EventItem>());

                var eventsProcessedWhileNoRemovals = eventsWhileNoRemovals
                    .ToObservableChangeSet(e => e.SequenceId)
                    .Filter(SequenceIdRange?.Select(CreateFilter) ?? Observable.Return<Func<EventItem, bool>>(_ => true))
                    .Batch(TimeSpan.FromMilliseconds(100))
                    .Sort(Utility.Comparer<EventItem>.ByKey(e => e.SequenceId))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler);

                Observable.Defer(() =>
                {
                    eventsCollection.Clear();

                    return eventsProcessedWhileNoRemovals
                        .ObserveOn(concurrencyService.DispatcherRxScheduler)
                        .Bind(eventsCollection, new SortedObservableCollectionAdaptor<EventItem, long>(int.MaxValue));
                })
                    .SubscribeOn(concurrencyService.DispatcherRxScheduler)
                    .Repeat()
                    .Subscribe()
                    .DisposeWith(disposables);
            });

            Func<EventItem, bool> CreateFilter((long start, long end) range)
            {
                return e => e.SequenceId >= range.start && e.SequenceId <= range.end;
            }
        }

        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }
        public IObservable<bool> IncludeInputObservables { get; set; }
        public IObservable<(long, long)> SequenceIdRange { get; set; }
        public IObservable<ClientEvent> ClientEvents { get; set; }

        public ReadOnlyObservableCollection<EventItem> Events { get; }
    }
}
