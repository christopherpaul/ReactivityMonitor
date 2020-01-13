using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Connection;
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
                var vmActivation = new SerialDisposable().DisposeWith(disposables);

                mEvent = selectionService.WhenSelectionChanges
                    .Select(x => new EventItemInfo(x.PrimaryEventItem, concurrencyService, ConnectionModel))
                    .Do(vm => vmActivation.Disposable = vm.Activator.Activate())
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .ToProperty(this, x => x.Event)
                    .DisposeWith(disposables);
            });
        }

        public IConnectionModel ConnectionModel { get; set; }

        private ObservableAsPropertyHelper<EventItemInfo> mEvent;
        public EventItemInfo Event => mEvent?.Value;

        public sealed class EventItemInfo : ReactiveViewModel
        {
            private readonly EventItem mItem;

            public EventItemInfo(EventItem item, IConcurrencyService concurrencyService, IConnectionModel connection)
            {
                mItem = item;

                this.WhenActivated((CompositeDisposable disposables) =>
                {
                    var payloadActivation = new SerialDisposable().DisposeWith(disposables);

                    mPayload = Observable.Never<PayloadObject>()
                        .StartWith(mItem?.Payload)
                        .OfType<PayloadObject>()
                        .Select(payload => new PayloadViewModel(payload, concurrencyService, connection))
                        .Do(vm => payloadActivation.Disposable = vm.Activator.Activate())
                        .ObserveOn(concurrencyService.DispatcherRxScheduler)
                        .ToProperty(this, x => x.Payload)
                        .DisposeWith(disposables);
                });
            }

            private ObservableAsPropertyHelper<PayloadViewModel> mPayload;
            public PayloadViewModel Payload => mPayload?.Value;
        }

        public sealed class PayloadViewModel : ReactiveViewModel
        {
            private readonly PayloadObject mPayload;

            public PayloadViewModel(PayloadObject payload, IConcurrencyService concurrencyService, IConnectionModel connection)
            {
                mPayload = payload;

                this.WhenActivated((CompositeDisposable disposables) =>
                {
                    connection.RequestObjectProperties(payload.ObjectId);

                    mProps = payload.Properties
                        .Select(ps => ps.Select(p => new PayloadProperty(p.Key, p.Value)).ToArray())
                        .ObserveOn(concurrencyService.DispatcherRxScheduler)
                        .ToProperty(this, x => x.Properties)
                        .DisposeWith(disposables);
                });
            }

            public string TypeName => mPayload.TypeName;

            private ObservableAsPropertyHelper<IReadOnlyList<PayloadProperty>> mProps;
            public IReadOnlyList<PayloadProperty> Properties => mProps?.Value;
        }

        public sealed class PayloadProperty
        {
            private readonly object mValue;

            public PayloadProperty(string name, object value)
            {
                Name = name;
                mValue = value;
            }

            public string Name { get; }
            public string Value => mValue?.ToString(); //TODO
        }
    }
}
