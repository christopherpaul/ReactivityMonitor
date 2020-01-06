using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    public sealed class StreamEventsViewModel : ReactiveViewModel
    {
        public StreamEventsViewModel(IConcurrencyService concurrencyService)
        {
            var streamEvents = new ObservableCollection<StreamEvent>();
            StreamEvents = new ReadOnlyObservableCollection<StreamEvent>(streamEvents);

            this.WhenActivated(observables =>
            {
                streamEvents.Clear();

                this.WhenAnyValue(x => x.StreamEventsSource)
                    .Where(source => source != null)
                    .Switch()
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Subscribe(streamEvents.Add)
                    .DisposeWith(observables);
            });
        }

        private IObservable<StreamEvent> mStreamEventSource;
        public IObservable<StreamEvent> StreamEventsSource
        {
            get => mStreamEventSource;
            set => this.RaiseAndSetIfChanged(ref mStreamEventSource, value);
        }

        public ReadOnlyObservableCollection<StreamEvent> StreamEvents { get; private set; }
    }
}
