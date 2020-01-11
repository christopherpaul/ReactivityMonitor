using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.EventListScreen;
using ReactivityMonitor.Services;

namespace ReactivityMonitor.Screens.PayloadScreen
{
    internal sealed class PayloadScreenViewModel : ReactiveViewModel, IPayloadScreen
    {
        public PayloadScreenViewModel(IConcurrencyService concurrencyService, ISelectionService selectionService)
        {
            this.WhenActivated((CompositeDisposable disposables) =>
            {
                mEvent = selectionService.WhenSelectionChanges
                    .Select(x => x.PrimaryEventItem)
                    .ToProperty(this, x => x.Event)
                    .DisposeWith(disposables);
            });
        }

        private ObservableAsPropertyHelper<EventItem> mEvent;
        public EventItem Event => mEvent?.Value;
    }
}
