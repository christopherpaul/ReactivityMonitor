using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.EventListScreen;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public sealed class Selection
    {
        public static Selection Empty { get; } = new Selection(default, default, default, default);

        private readonly Selected<IInstrumentedCall> mCallSelection;
        private readonly Selected<IObservableInstance> mObservableSelection;
        private readonly Selected<EventItem> mEventSelection;

        private Selection(IWorkspace workspace, Selected<IInstrumentedCall> callSelection, Selected<IObservableInstance> observableSelection, Selected<EventItem> eventSelection)
        {
            Workspace = workspace;
            mCallSelection = callSelection;
            mObservableSelection = observableSelection;
            mEventSelection = eventSelection;
        }

        public Selection SetWorkspace(IWorkspace workspace)
        {
            if (Workspace == workspace)
            {
                return this;
            }

            return new Selection(workspace, mCallSelection, mObservableSelection, mEventSelection);
        }

        public Selection SetCall(IInstrumentedCall call)
        {
            return ChangeCalls(mCallSelection.Single(call));
        }

        public Selection AddCall(IInstrumentedCall call)
        {
            return ChangeCalls(mCallSelection.Add(call));
        }

        public Selection RemoveCall(IInstrumentedCall call)
        {
            return ChangeCalls(mCallSelection.Remove(call));
        }

        public Selection ClearCall()
        {
            return ChangeCalls(default);
        }

        private Selection ChangeCalls(Selected<IInstrumentedCall> callSelection)
        {
            return new Selection(Workspace, callSelection, mObservableSelection, mEventSelection);
        }

        public Selection SetObservableInstance(IObservableInstance observable)
        {
            return ChangeObservables(mObservableSelection.Single(observable));
        }

        public Selection AddObservableInstance(IObservableInstance observable)
        {
            return ChangeObservables(mObservableSelection.Add(observable));
        }

        public Selection RemoveObservableInstance(IObservableInstance observable)
        {
            return ChangeObservables(mObservableSelection.Remove(observable));
        }

        public Selection ClearObservableInstances()
        {
            return ChangeObservables(default);
        }

        private Selection ChangeObservables(Selected<IObservableInstance> observableSelection)
        {
            return new Selection(Workspace, mCallSelection, observableSelection, mEventSelection);
        }

        public Selection SetEvent(EventItem eventItem)
        {
            return ChangeEvents(mEventSelection.Single(eventItem));
        }

        public Selection AddEvent(EventItem eventItem)
        {
            return ChangeEvents(mEventSelection.Add(eventItem));
        }

        public Selection RemoveEvent(EventItem eventItem)
        {
            return ChangeEvents(mEventSelection.Remove(eventItem));
        }

        public Selection ClearEvent()
        {
            return ChangeEvents(default);
        }

        private Selection ChangeEvents(Selected<EventItem> eventSelection)
        {
            return new Selection(Workspace, mCallSelection, mObservableSelection, eventSelection);
        }

        public IWorkspace Workspace { get; }
        public IInstrumentedCall PrimaryInstrumentedCall => mCallSelection.Primary;
        public IImmutableList<IInstrumentedCall> SelectedInstrumentedCalls => mCallSelection.All;
        public IObservableInstance PrimaryObservableInstance => mObservableSelection.Primary;
        public IImmutableList<IObservableInstance> SelectedObservableInstances => mObservableSelection.All;
        public EventItem PrimaryEventItem => mEventSelection.Primary;
        public IImmutableList<EventItem> SelectedEventItems => mEventSelection.All;

        private struct Selected<T> where T : class
        {
            private readonly IImmutableList<T> mAll;

            private Selected(T primary, IImmutableList<T> all)
            {
                Primary = primary;
                mAll = all;
            }

            public T Primary { get; }
            public IImmutableList<T> All => mAll ?? ImmutableList<T>.Empty;

            public Selected<T> None => default;
            public Selected<T> Single(T item) => new Selected<T>(item, ImmutableList.Create(item));
            public Selected<T> Add(T item) => item == null ? this : new Selected<T>(item, All.Add(item));
            public Selected<T> Remove(T item) => item == null ? this : new Selected<T>(item == Primary ? null : Primary, All.Remove(item));
        }
    }
}
