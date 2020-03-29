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
        public static Selection Empty { get; } = new Selection(default, default, default, default, default);

        private readonly Selected<IInstrumentedCall> mCallSelection;
        private readonly Selected<IObservableInstance> mObservableSelection;
        private readonly Selected<EventItem> mEventSelection;
        private readonly Selected<ISubscription> mSubscriptionSelection;

        private Selection(
            IWorkspace workspace, 
            Selected<IInstrumentedCall> callSelection, 
            Selected<IObservableInstance> observableSelection, 
            Selected<ISubscription> subscriptionSelection,
            Selected<EventItem> eventSelection)
        {
            Workspace = workspace;
            mCallSelection = callSelection;
            mObservableSelection = observableSelection;
            mSubscriptionSelection = subscriptionSelection;
            mEventSelection = eventSelection;
        }

        public Selection SetWorkspace(IWorkspace workspace)
        {
            if (Workspace == workspace)
            {
                return this;
            }

            return new Selection(workspace, mCallSelection, mObservableSelection, mSubscriptionSelection, mEventSelection);
        }

        public Selection SetCall(IInstrumentedCall call) => ChangeCalls(mCallSelection.Single(call));
        public Selection AddCall(IInstrumentedCall call) => ChangeCalls(mCallSelection.Add(call));
        public Selection RemoveCall(IInstrumentedCall call) => ChangeCalls(mCallSelection.Remove(call));
        public Selection ClearCall() => ChangeCalls(default);

        private Selection ChangeCalls(Selected<IInstrumentedCall> callSelection)
        {
            return new Selection(Workspace, callSelection, mObservableSelection, mSubscriptionSelection, mEventSelection);
        }

        public Selection SetObservableInstance(IObservableInstance observable) => ChangeObservables(mObservableSelection.Single(observable));
        public Selection AddObservableInstance(IObservableInstance observable) => ChangeObservables(mObservableSelection.Add(observable));
        public Selection RemoveObservableInstance(IObservableInstance observable) => ChangeObservables(mObservableSelection.Remove(observable));
        public Selection ClearObservableInstances() => ChangeObservables(default);

        private Selection ChangeObservables(Selected<IObservableInstance> observableSelection)
        {
            return new Selection(Workspace, mCallSelection, observableSelection, mSubscriptionSelection, mEventSelection);
        }

        public Selection SetSubscription(ISubscription subscription) => ChangeSubscriptions(mSubscriptionSelection.Single(subscription));
        public Selection AddSubscription(ISubscription subscription) => ChangeSubscriptions(mSubscriptionSelection.Add(subscription));
        public Selection RemoveSubscription(ISubscription subscription) => ChangeSubscriptions(mSubscriptionSelection.Remove(subscription));
        public Selection ClearSubscriptions() => ChangeSubscriptions(default);

        private Selection ChangeSubscriptions(Selected<ISubscription> subscriptionSelection)
        {
            return new Selection(Workspace, mCallSelection, mObservableSelection, subscriptionSelection, mEventSelection);
        }

        public Selection SetEvent(EventItem eventItem) => ChangeEvents(mEventSelection.Single(eventItem));
        public Selection AddEvent(EventItem eventItem) => ChangeEvents(mEventSelection.Add(eventItem));
        public Selection RemoveEvent(EventItem eventItem) => ChangeEvents(mEventSelection.Remove(eventItem));
        public Selection ClearEvent() => ChangeEvents(default);

        private Selection ChangeEvents(Selected<EventItem> eventSelection)
        {
            return new Selection(Workspace, mCallSelection, mObservableSelection, mSubscriptionSelection, eventSelection);
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
