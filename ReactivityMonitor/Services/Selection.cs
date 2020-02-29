using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.EventListScreen;
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
        public static Selection Empty { get; } = new Selection(null, ImmutableList<EventItem>.Empty, null, ImmutableList<IInstrumentedCall>.Empty);

        private Selection(EventItem primaryEventItem, IImmutableList<EventItem> selectedEventItems,
            IInstrumentedCall primaryInstrumentedCall, IImmutableList<IInstrumentedCall> selectedInstrumentedCalls)
        {
            PrimaryEventItem = primaryEventItem;
            SelectedEventItems = selectedEventItems;
            PrimaryInstrumentedCall = primaryInstrumentedCall;
            SelectedInstrumentedCalls = selectedInstrumentedCalls;
        }

        public Selection SetEvent(EventItem eventItem)
        {
            return ChangeEvents(eventItem, ImmutableList.Create(eventItem));
        }

        public Selection AddEvent(EventItem eventItem)
        {
            return ChangeEvents(eventItem, SelectedEventItems.Add(eventItem));
        }

        public Selection RemoveEvent(EventItem eventItem)
        {
            return ChangeEvents(eventItem == PrimaryEventItem ? null : PrimaryEventItem, SelectedEventItems.Remove(eventItem));
        }

        public Selection ClearEvent()
        {
            return ChangeEvents(null, Empty.SelectedEventItems);
        }

        private Selection ChangeEvents(EventItem primary, IImmutableList<EventItem> selected)
        {
            return new Selection(primary, selected, PrimaryInstrumentedCall, SelectedInstrumentedCalls);
        }

        public Selection SetCall(IInstrumentedCall call)
        {
            return ChangeCalls(call, ImmutableList.Create(call));
        }

        public Selection AddCall(IInstrumentedCall call)
        {
            return ChangeCalls(call, SelectedInstrumentedCalls.Add(call));
        }

        public Selection RemoveCall(IInstrumentedCall call)
        {
            return ChangeCalls(call == PrimaryInstrumentedCall ? null : PrimaryInstrumentedCall,
                SelectedInstrumentedCalls.Remove(call));
        }

        public Selection ClearCall()
        {
            return ChangeCalls(null, Empty.SelectedInstrumentedCalls);
        }

        private Selection ChangeCalls(IInstrumentedCall primary, IImmutableList<IInstrumentedCall> selected)
        {
            return new Selection(PrimaryEventItem, SelectedEventItems, primary, selected);
        }

        public EventItem PrimaryEventItem { get; }
        public IImmutableList<EventItem> SelectedEventItems { get; }
        public IInstrumentedCall PrimaryInstrumentedCall { get; }
        public IImmutableList<IInstrumentedCall> SelectedInstrumentedCalls { get; }
    }
}
