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
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public sealed class EventListViewModel : ReactiveViewModel, IEventList
    {
        private readonly Action<Func<Selection, Selection>> mSubmitSelectionChange;

        public EventListViewModel(IConcurrencyService concurrencyService)
        {
            var eventsCollection = new ObservableCollectionExtended<EventItem>();
            Events = new ReadOnlyObservableCollection<EventItem>(eventsCollection);

            var selectionChangeSubject = new Subject<Func<Selection, Selection>>();
            mSubmitSelectionChange = selectionChangeSubject.OnNext;
            WhenEventSelectionChanges = selectionChangeSubject
                .ObserveOn(concurrencyService.TaskPoolRxScheduler);

            this.WhenActivated((CompositeDisposable disposables) => 
            {
                var whenIncludeInputsChanges = 
                    IncludeInputObservables
                        ?.ObserveOn(concurrencyService.TaskPoolRxScheduler)
                        .DistinctUntilChanged()
                    ?? Observable.Return(false);

                var eventsProcessedUntilInvalid =
                    whenIncludeInputsChanges.Publish(whenIncludeInputsChangesPub =>
                        whenIncludeInputsChangesPub.SelectMany(includeInputs =>
                            Observables
                                .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                                .Replay(observablesPub =>
                                {
                                    var observablesExpanded =
                                        observablesPub
                                            .MergeMany(obs => Observable.Return(obs))
                                            .ExpandDistinct(obs => includeInputs ? obs.Inputs : Observable.Empty<IObservableInstance>());

                                    var events = observablesExpanded
                                        .SelectMany(obs => Observable.Return(EventItem.FromObservableInstance(obs))
                                            .Concat(obs.Subscriptions.SelectMany(sub => sub.Events.Select(e => EventItem.FromStreamEvent(sub, e)))))
                                        .Merge(ClientEvents?.Select(EventItem.FromClientEvent) ?? Observable.Empty<EventItem>());

                                    var eventsProcessed = events
                                        .ToObservableChangeSet(e => e.SequenceId)
                                        .Filter(SequenceIdRange?.Select(CreateFilter) ?? Observable.Return<Func<EventItem, bool>>(_ => true))
                                        .Batch(TimeSpan.FromMilliseconds(100))
                                        .Sort(Utility.Comparer<EventItem>.ByKey(e => e.SequenceId));

                                    // Terminate the stream if an observable is removed or the include inputs
                                    // flag changes, as in both cases we need to rebuild the output from scratch.
                                    return eventsProcessed
                                        .TakeUntil(observablesPub.Where(chg => chg.Removes > 0))
                                        .TakeUntil(whenIncludeInputsChangesPub.Where(ii => ii != includeInputs));
                                }))
                    )
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler);

                Observable.Defer(() =>
                {
                    eventsCollection.Clear();

                    return eventsProcessedUntilInvalid
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

        public void SubmitSelectionChange(Func<Selection, Selection> selectionChanger) => mSubmitSelectionChange(selectionChanger);

        public IObservable<Func<Selection, Selection>> WhenEventSelectionChanges { get; }
    }
}
