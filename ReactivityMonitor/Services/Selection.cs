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
        public static Selection Empty { get; } = new Selection(default, default, default);

        private readonly Selected<IInstrumentedCall> mCallSelection;
        private readonly Selected<EventItem> mEventSelection;

        private Selection(IWorkspace workspace, Selected<IInstrumentedCall> callSelection, Selected<EventItem> eventSelection)
        {
            Workspace = workspace;
            mCallSelection = callSelection;
            mEventSelection = eventSelection;
        }

        public Selection SetWorkspace(IWorkspace workspace)
        {
            if (Workspace == workspace)
            {
                return this;
            }

            return new Selection(workspace, mCallSelection, mEventSelection);
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
            return new Selection(Workspace, mCallSelection, eventSelection);
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
            return new Selection(Workspace, callSelection, mEventSelection);
        }

        public IWorkspace Workspace { get; }
        public EventItem PrimaryEventItem => mEventSelection.Primary;
        public IImmutableList<EventItem> SelectedEventItems => mEventSelection.All;
        public IInstrumentedCall PrimaryInstrumentedCall => mCallSelection.Primary;
        public IImmutableList<IInstrumentedCall> SelectedInstrumentedCalls => mCallSelection.All;

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
