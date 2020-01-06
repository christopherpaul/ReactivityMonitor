using DynamicData;
using DynamicData.Binding;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class MarbleObservableItemGroup : ReactiveViewModel
    {
        private readonly IInstrumentedCall mCall;

        public MarbleObservableItemGroup(IImmutableList<long> ordering, IInstrumentedCall call, ReadOnlyObservableCollection<MarbleObservableItem> items, IConcurrencyService concurrencyService)
        {
            Ordering = ordering;
            mCall = call;
            Items = items;

            StreamEventsViewModel = new StreamEventsViewModel(concurrencyService);
            StreamEventsViewModel.StreamEventsSource = Items.ToObservableChangeSet()
                .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                .AddKey(x => x.ObservableInstance.ObservableId)
                .WhereReasonsAre(ChangeReason.Add)
                .Flatten()
                .Select(chg => chg.Current)
                .Select(item => item.ObservableInstance)
                .SelectMany(obs => obs.Subscriptions)
                .SelectMany(sub => sub.Events);
        }

        public string ShortName => mCall.CalledMethod;
        public string LongName => $"{mCall.CallingType}.{mCall.CallingMethod}: {mCall.CalledMethod}";
        public IImmutableList<long> Ordering { get; }
        public ReadOnlyObservableCollection<MarbleObservableItem> Items { get; }

        public StreamEventsViewModel StreamEventsViewModel { get; set; }
    }
}
