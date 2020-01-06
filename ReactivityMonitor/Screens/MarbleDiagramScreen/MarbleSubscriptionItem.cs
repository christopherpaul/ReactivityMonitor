using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class MarbleSubscriptionItem : ReactiveViewModel
    {
        public MarbleSubscriptionItem(IConcurrencyService concurrencyService)
        {
            StreamEventsViewModel = new StreamEventsViewModel(concurrencyService);

            this.WhenActivated((CompositeDisposable _) =>
            {
                StreamEventsViewModel.StreamEventsSource = Subscription.Events;
            });
        }

        public ISubscription Subscription { get; set; }

        public string HeaderText => $"{Subscription.SubscriptionId}";

        public StreamEventsViewModel StreamEventsViewModel { get; }
    }
}
