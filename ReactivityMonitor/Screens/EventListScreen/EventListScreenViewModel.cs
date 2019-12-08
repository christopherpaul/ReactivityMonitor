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

namespace ReactivityMonitor.Screens.EventListScreen
{
    public sealed class EventListScreenViewModel : ReactiveScreen, IEventListScreen
    {
        public EventListScreenViewModel(IConcurrencyService concurrencyService)
        {
            WhenActivated(disposables =>
            {
                var isUpdatingSubject = new BehaviorSubject<bool>(true);
                var clearSubject = new Subject<Unit>();

                var pauseCommand = ReactiveCommand.Create(() => isUpdatingSubject.OnNext(false), isUpdatingSubject);
                var goCommand = ReactiveCommand.Create(() => isUpdatingSubject.OnNext(true), isUpdatingSubject.Select(x => !x));
                var clearCommand = ReactiveCommand.Create(() => clearSubject.OnNext(default));

                var allEvents = Model.ObservableInstances
                    .SelectMany(obs =>
                        Observable.Return(EventItem.FromObservableInstance(obs))
                            .Concat(obs.Subscriptions.SelectMany(sub => sub.Events.Select(e => EventItem.FromStreamEvent(sub, e)))));

                allEvents
                    .Window(clearSubject)
                    .Select(eventsSinceClear => 
                        isUpdatingSubject
                            .DistinctUntilChanged()
                            .Publish(isUpdatingSafe =>
                                eventsSinceClear
                                    .Window(isUpdatingSafe)
                                    .Zip(isUpdatingSafe.StartWith(false), (window, isUpdating) =>
                                        isUpdating
                                            ? window
                                            : window.Buffer(Observable.Never<Unit>()).SelectMany(buf => buf))
                                    .Concat())
                        .AsChangeSets()
                        .Sort(Utility.Comparer<EventItem>.ByKey(e => e.SequenceId)))
                    .Switch()
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var eventsCollection)
                    .Subscribe()
                    .DisposeWith(disposables);

                Events = eventsCollection;

                Pause = pauseCommand;
                Go = goCommand;
                Clear = clearCommand;
            });
        }

        public IReactivityModel Model { get; set; }

        public ReadOnlyObservableCollection<EventItem> Events { get; private set; }

        public ICommand Pause { get; private set; }
        public ICommand Go { get; private set; }
        public ICommand Clear { get; private set; }
    }
}
