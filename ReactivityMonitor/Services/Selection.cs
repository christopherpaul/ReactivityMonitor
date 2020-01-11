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
        public static Selection Empty { get; } = new Selection(null, ImmutableList<EventItem>.Empty);

        private Selection(EventItem primaryEventItem, IImmutableList<EventItem> selectedEventItems)
        {
            PrimaryEventItem = primaryEventItem;
            SelectedEventItems = selectedEventItems;
        }

        public Selection Set(EventItem eventItem)
        {
            return new Selection(eventItem, ImmutableList.Create(eventItem));
        }

        public Selection Add(EventItem eventItem)
        {
            return new Selection(eventItem, SelectedEventItems.Add(eventItem));
        }

        public Selection Remove(EventItem eventItem)
        {
            return new Selection(null, SelectedEventItems.Remove(eventItem));
        }

        public EventItem PrimaryEventItem { get; }
        public IImmutableList<EventItem> SelectedEventItems { get; }
    }
}
