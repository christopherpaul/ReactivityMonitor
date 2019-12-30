﻿using DynamicData;
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
using ReactivityMonitor.Definitions;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public sealed class EventListScreenViewModel : ReactiveScreen, IEventListScreen
    {
        public EventListScreenViewModel(IConcurrencyService concurrencyService, ICommandHandlerService commandHandlerService)
        {
            var filterCommand = ReactiveCommand.Create<bool, bool>(filter => filter);
            var whenIsFilteringToActiveGroupChanges = filterCommand.ObserveOn(concurrencyService.TaskPoolRxScheduler)
                .StartWith(false)
                .Replay(1);

            var whenClearCommandExecuted = CommandHelper.CreateTriggerCommand(out var clearCommand);

            WhenActivated(disposables =>
            {
                var activeGroupObservableInstances = WhenActiveMonitoringGroupChanges
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .Select(group =>
                        group.Calls
                            .MergeMany(call => call.Call.ObservableInstances)
                            .Expand(obs => obs.Inputs)
                            .ToObservableChangeSet(obs => obs.ObservableId))
                    .Switch();

                var allObservableInstances = Model.ObservableInstances.ToObservableChangeSet(obs => obs.ObservableId);

                var filterObservableInstances = whenIsFilteringToActiveGroupChanges
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .Select(isFilteringToActiveGroup => isFilteringToActiveGroup ? activeGroupObservableInstances : allObservableInstances)
                    .Switch();

                var allEvents = Model.ObservableInstances
                    .SelectMany(obs =>
                        Observable.Return(EventItem.FromObservableInstance(obs))
                            .Concat(obs.Subscriptions.SelectMany(sub => sub.Events.Select(e => EventItem.FromStreamEvent(sub, e)))));

                allEvents
                    .Window(OnTaskPool(whenClearCommandExecuted))
                    .Select(eventsSinceClear => eventsSinceClear
                        .Gate(WhenIsUpdatingChanges)
                        .ToObservableChangeSet(e => e.SequenceId)
                        .SemiJoinOnRightKey(filterObservableInstances, e => e.ObservableId))
                    .Switch()
                    .Batch(TimeSpan.FromMilliseconds(100))
                    .Sort(Utility.Comparer<EventItem>.ByKey(e => e.SequenceId))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var eventsCollection, resetThreshold: int.MaxValue)
                    .Subscribe()
                    .DisposeWith(disposables);

                Events = eventsCollection;

                commandHandlerService.RegisterHandler(Commands.ClearEventList, clearCommand).DisposeWith(disposables);
                commandHandlerService.RegisterHandler(Commands.FilterEventList, filterCommand).DisposeWith(disposables);

                whenIsFilteringToActiveGroupChanges.Connect().DisposeWith(disposables);
            });

            IObservable<Unit> OnTaskPool(IObservable<Unit> obs) => obs.ObserveOn(concurrencyService.TaskPoolRxScheduler);
        }

        public IReactivityModel Model { get; set; }
        public IObservable<IMonitoringGroup> WhenActiveMonitoringGroupChanges { get; set; }

        public ReadOnlyObservableCollection<EventItem> Events { get; private set; }

        public IObservable<bool> WhenIsUpdatingChanges { get; set; }
    }
}
