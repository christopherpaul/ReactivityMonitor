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
using ReactivityMonitor.Definitions;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public sealed class EventListScreenViewModel : ReactiveViewModel, IWorkspaceDocumentScreenBuilder<IEventsDocument>
    {
        private IEventsDocument mDocument;

        public EventListScreenViewModel(
            IConcurrencyService concurrencyService, 
            IEventList eventList)
        {
            EventList = eventList;

            this.WhenActivated(disposables =>
            {
                var observablesForCalls = mDocument.Calls
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .TakeWhile(change => change.Removes == 0) // stop if any calls removed
                    .MergeMany(call => call.ObservableInstances)
                    .ToObservableChangeSet(obs => obs.ObservableId)
                    .Repeat(); // reset after calls removed

                eventList.Observables = mDocument.Observables.Or(observablesForCalls);

                eventList.Activator.Activate().DisposeWith(disposables);
            });
        }

        public string DisplayName => mDocument.DocumentName;

        public IEventList EventList { get; }

        public IWorkspaceDocument Document => mDocument;

        public void SetDocument(IEventsDocument document)
        {
            mDocument = document;
        }
    }
}
