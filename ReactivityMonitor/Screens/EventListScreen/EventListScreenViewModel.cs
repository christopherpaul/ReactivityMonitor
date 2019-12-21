using DynamicData;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Utility.Extensions;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Utility;
using System.Reactive.Disposables;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive.Subjects;
using System.Reactive;
using ReactivityMonitor.Workspace;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public sealed class EventListScreenViewModel : ReactiveScreen, IEventListScreen
    {
        public EventListScreenViewModel(IConcurrencyService concurrencyService)
        {
            var whenIsFilteringToActiveGroupChanges = this.WhenAnyValue(x => x.IsFilteringToActiveGroup).ObserveOn(concurrencyService.TaskPoolRxScheduler);

            WhenActivated(disposables =>
            {
                var isUpdating = new ObservablePromise<bool>();

                var pauseCommand = CreateCommand(isUpdating);
                var goCommand = CreateCommand(isUpdating.Select(x => !x));
                var clearCommand = CreateCommand();

                isUpdating.Resolve(
                    OnTaskPool(pauseCommand).Select(_ => false)
                        .Merge(OnTaskPool(goCommand).Select(_ => true))
                        .StartWith(true));

                var activeGroupObservableInstances =
                    WhenActiveMonitoringGroupChanges.Select(group =>
                        group.Calls
                            .MergeMany(call => call.Call.ObservableInstances)
                            .Expand(obs => obs.Inputs)
                            .ToObservableChangeSet(obs => obs.ObservableId))
                    .Switch();

                var allObservableInstances = Model.ObservableInstances.ToObservableChangeSet(obs => obs.ObservableId);

                var filterObservableInstances = whenIsFilteringToActiveGroupChanges
                    .Select(isFilteringToActiveGroup => isFilteringToActiveGroup ? activeGroupObservableInstances : allObservableInstances)
                    .Switch();

                var allEvents = Model.ObservableInstances
                    .SelectMany(obs =>
                        Observable.Return(EventItem.FromObservableInstance(obs))
                            .Concat(obs.Subscriptions.SelectMany(sub => sub.Events.Select(e => EventItem.FromStreamEvent(sub, e)))));

                allEvents
                    .Window(OnTaskPool(clearCommand))
                    .Select(eventsSinceClear => 
                        isUpdating
                            .DistinctUntilChanged()
                            .Publish(isUpdatingSafe =>
                                eventsSinceClear
                                    .Window(isUpdatingSafe)
                                    .Zip(isUpdatingSafe.StartWith(false), (window, isUpdating) =>
                                        isUpdating
                                            ? window
                                            : window.Buffer(Observable.Never<Unit>()).SelectMany(buf => buf))
                                    .Concat())
                        .ToObservableChangeSet(e => e.SequenceId)
                        .SemiJoinOnRightKey(filterObservableInstances, e => e.ObservableId)
                        .Sort(Utility.Comparer<EventItem>.ByKey(e => e.SequenceId)))
                    .Switch()
                    .Batch(TimeSpan.FromSeconds(1))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var eventsCollection)
                    .Subscribe()
                    .DisposeWith(disposables);

                Events = eventsCollection;

                Pause = pauseCommand;
                Go = goCommand;
                Clear = clearCommand;
            });

            ReactiveCommand<Unit, Unit> CreateCommand(IObservable<bool> canExecute = null) => 
                ReactiveCommand.Create(() => { }, canExecute?.ObserveOn(concurrencyService.DispatcherRxScheduler));

            IObservable<Unit> OnTaskPool(IObservable<Unit> obs) => obs.ObserveOn(concurrencyService.TaskPoolRxScheduler);
        }

        public IReactivityModel Model { get; set; }
        public IObservable<IMonitoringGroup> WhenActiveMonitoringGroupChanges { get; set; }

        public ReadOnlyObservableCollection<EventItem> Events { get; private set; }

        public ICommand Pause { get; private set; }
        public ICommand Go { get; private set; }
        public ICommand Clear { get; private set; }

        private bool mIsFilteringToActiveGroup;
        public bool IsFilteringToActiveGroup
        {
            get => mIsFilteringToActiveGroup;
            set => Set(ref mIsFilteringToActiveGroup, value);
        }
    }
}
