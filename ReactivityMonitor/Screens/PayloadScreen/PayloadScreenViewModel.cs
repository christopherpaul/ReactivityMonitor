using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Select(x => new EventItemInfo(x.PrimaryEventItem, concurrencyService, ConnectionModel))
                    .Do(vm => vmActivation.Disposable = vm.Activator.Activate())
                    .ToProperty(this, x => x.Event)
                    .DisposeWith(disposables);
            });
        }

        public IConnectionModel ConnectionModel { get; set; }

        private ObservableAsPropertyHelper<EventItemInfo> mEvent;
        public EventItemInfo Event => mEvent?.Value;

        public sealed class EventItemInfo : ReactiveViewModel
        {
            public EventItemInfo(EventItem item, IConcurrencyService concurrencyService, IConnectionModel connection)
            {
                var whenGoToParentInvoked = CommandHelper.CreateTriggerCommand(out var goToParentCommand);
                GoToParentObjectCommand = goToParentCommand;

                this.WhenActivated((CompositeDisposable disposables) =>
                {
                    if (item?.Payload != null)
                    {
                        var payloadActivation = new SerialDisposable().DisposeWith(disposables);

                        var inspectSubject = new Subject<PayloadObject>();

                        var whenUserDrillsIntoObject = inspectSubject
                            .OfType<PayloadObject>()
                            .Select(payload => new Func<IImmutableStack<PayloadObject>, IImmutableStack<PayloadObject>>(s => s.Push(payload)));

                        Func<IImmutableStack<PayloadObject>, IImmutableStack<PayloadObject>> pop = s => s.IsEmpty ? s : s.Pop();
                        var whenUserComesOutOfObject = whenGoToParentInvoked
                            .Select(_ => pop);

                        IImmutableStack<PayloadObject> emptyStack = ImmutableStack<PayloadObject>.Empty;

                        mPayload = new[] { whenUserDrillsIntoObject, whenUserComesOutOfObject }.Merge()
                            .Scan(emptyStack, (s, xfm) => xfm(s))
                            .StartWith(emptyStack)
                            .Select(s => s.IsEmpty ? item.Payload : s.Peek())
                            .Where(payload => payload != null)
                            .Select(payload => new PayloadViewModel(payload, inspectSubject.OnNext, concurrencyService, connection))
                            .Do(vm => payloadActivation.Disposable = vm.Activator.Activate())
                            .ObserveOn(concurrencyService.DispatcherRxScheduler)
                            .ToProperty(this, x => x.Payload)
                            .DisposeWith(disposables);
                    }
                });
            }

            private ObservableAsPropertyHelper<PayloadViewModel> mPayload;
            public PayloadViewModel Payload => mPayload?.Value;
            public ICommand GoToParentObjectCommand { get; }
        }

        public sealed class PayloadViewModel : ReactiveViewModel
        {
            private readonly PayloadObject mPayload;

            public PayloadViewModel(PayloadObject payload, Action<PayloadObject> inspect, IConcurrencyService concurrencyService, IConnectionModel connection)
            {
                mPayload = payload;

                this.WhenActivated((CompositeDisposable disposables) =>
                {
                    connection.RequestObjectProperties(payload.ObjectId);

                    mProps = payload.Properties
                        .Select(ps => ps.Select(p => new PayloadProperty(p.Key, p.Value, inspect)).ToArray())
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

            public PayloadProperty(string name, object value, Action<PayloadObject> inspect)
            {
                Name = name;
                mValue = value;
                if (value is PayloadObject child)
                {
                    InspectValueCommand = ReactiveCommand.Create(() => inspect(child));
                }
            }

            public string Name { get; }
            public string ValueString => mValue?.ToString(); //TODO
            public bool IsExceptionGettingValue => mValue is PayloadObject payload && payload.IsExceptionGettingValue;
            public ICommand InspectValueCommand { get; }
        }
    }
}
