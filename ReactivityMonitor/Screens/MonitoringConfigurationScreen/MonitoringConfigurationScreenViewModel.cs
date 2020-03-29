using DynamicData;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Utility.Extensions;
using ReactivityMonitor.Services;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.Windows.Input;

namespace ReactivityMonitor.Screens.MonitoringConfigurationScreen
{
    public sealed class MonitoringConfigurationScreenViewModel : ReactiveViewModel, IWorkspaceDocumentScreenBuilder<IMonitoringConfiguration>
    {
        private IMonitoringConfiguration mDocument;

        public MonitoringConfigurationScreenViewModel(IConcurrencyService concurrencyService, ISelectionService selectionService)
        {
            var methodItems = new ObservableCollectionExtended<MethodItem>();
            Methods = new ReadOnlyObservableCollection<MethodItem>(methodItems);

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                methodItems.Clear();

                var monitoredCalls = Workspace.MonitoredCalls
                    .TakeUntilDisposed(disposables)
                    .Transform(c => c.Call)
                    .AddKey(c => c.InstrumentedCallId)
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .AsObservableCache();

                Workspace.Methods
                    .Transform(method => new MethodItem(method, monitoredCalls, concurrencyService, Workspace))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(methodItems)
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);

                this.WhenValueChanged(x => x.SelectedItem)
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .Subscribe(item =>
                    {
                        if (item is CallItem c)
                        {
                            selectionService.ChangeSelection(s => s.SetCall(c.InstrumentedCall));
                        }
                        else
                        {
                            selectionService.ChangeSelection(s => s.ClearCall());
                        }
                    });
            });
        }

        public string DisplayName => "Configuration";

        private object mSelectedItem;
        public object SelectedItem
        {
            get => mSelectedItem;
            set => this.RaiseAndSetIfChanged(ref mSelectedItem, value);
        }

        public IWorkspaceDocument Document => mDocument;
        private IWorkspace Workspace => Document?.Workspace;

        public ReadOnlyObservableCollection<MethodItem> Methods { get; }

        public sealed class MethodItem : ReactiveObject, IDisposable
        {
            private readonly CompositeDisposable mDisposables;
            private readonly IInstrumentedMethod mMethod;

            public MethodItem(IInstrumentedMethod method, IObservableCache<IInstrumentedCall, int> monitoredCalls, IConcurrencyService concurrencyService, IWorkspace workspace)
            {
                mDisposables = new CompositeDisposable();

                var callsWithIsMonitoredChanges = method.InstrumentedCalls
                    .Select(call => new { Call = call, WhenIsMonitoredChanges = GetWhenIsMonitoredChanges(call) })
                    .ToImmutableList();

                Calls = callsWithIsMonitoredChanges
                    .Select(x => new CallItem(x.Call, x.WhenIsMonitoredChanges.ObserveOn(concurrencyService.DispatcherRxScheduler), workspace))
                    .ToImmutableList();

                var canStartMonitoring = Observable.CombineLatest(
                    callsWithIsMonitoredChanges.Select(x => x.WhenIsMonitoredChanges),
                    list => list.Contains(false)).ObserveOn(concurrencyService.DispatcherRxScheduler);

                var canStopMonitoring = Observable.CombineLatest(
                    callsWithIsMonitoredChanges.Select(x => x.WhenIsMonitoredChanges),
                    list => list.Contains(true)).ObserveOn(concurrencyService.DispatcherRxScheduler);

                StartMonitoringAllCommand = ReactiveCommand.Create(StartMonitoringAllCalls, canStartMonitoring);
                StopMonitoringAllCommand = ReactiveCommand.Create(StopMonitoringAllCalls, canStopMonitoring);

                mMethod = method;

                IObservable<bool> GetWhenIsMonitoredChanges(IInstrumentedCall c)
                {
                    return monitoredCalls.Watch(c.InstrumentedCallId)
                        .TakeUntilDisposed(mDisposables)
                        .Select(chg => chg.Reason != ChangeReason.Remove)
                        .StartWith(false)
                        .DistinctUntilChanged()
                        .Replay(1)
                        .ConnectForEver();
                }

                void StartMonitoringAllCalls()
                {
                    foreach (var call in method.InstrumentedCalls)
                    {
                        workspace.StartMonitoringCall(call);
                    }
                }

                void StopMonitoringAllCalls()
                {
                    foreach (var call in method.InstrumentedCalls)
                    {
                        workspace.StopMonitoringCall(call);
                    }
                }
            }

            public string TypeName => mMethod.ParentType;
            public string MethodName => mMethod.Name;

            public ICommand StartMonitoringAllCommand { get; }
            public ICommand StopMonitoringAllCommand { get; }

            public IImmutableList<CallItem> Calls { get; }

            public void Dispose()
            {
                mDisposables.Dispose();
            }

            private bool mIsExpanded = true;
            public bool IsExpanded
            {
                get => mIsExpanded;
                set => this.RaiseAndSetIfChanged(ref mIsExpanded, value);
            }
        }

        public sealed class CallItem : ReactiveViewModel
        {
            private readonly IInstrumentedCall mCall;

            public CallItem(IInstrumentedCall call, IObservable<bool> whenIsMonitoredChanges, IWorkspace workspace)
            {
                mCall = call;

                var isMonitoredWhileActive = Observable.Merge(
                    Activator.Activated.Select(_ => whenIsMonitoredChanges),
                    Activator.Deactivated.Select(_ => Observable.Empty<bool>()))
                    .Switch()
                    .Publish(default)
                    .RefCount();

                StartMonitoringCommand = ReactiveCommand.Create(() => workspace.StartMonitoringCall(call), isMonitoredWhileActive.Select(x => !x));
                StopMonitoringCommand = ReactiveCommand.Create(() => workspace.StopMonitoringCall(call), isMonitoredWhileActive);
                
                this.WhenActivated((CompositeDisposable disposables) =>
                {
                    var activeSubs = call.ObservableInstances
                        .SelectMany(obs => obs.Subscriptions)
                        .SelectMany(sub => Observable.Return(1).Concat(sub.Events.TakeUntil(IsDeactivationEvent).WhenTerminated(-1)))
                        .Scan(0, (tally, x) => tally + x);

                    mActiveSubscriptionCount = whenIsMonitoredChanges
                        .Select(isMonitored => isMonitored
                            ? activeSubs
                            : Observable.Return(-1))
                        .Switch()
                        .Select(count => count >= 0 ? $"[{count}]" : string.Empty)
                        .ToProperty(this, x => x.ActiveSubscriptionCount);
                });

                bool IsDeactivationEvent(StreamEvent e) =>
                    e.Kind == StreamEvent.EventKind.OnCompleted ||
                    e.Kind == StreamEvent.EventKind.OnError ||
                    e.Kind == StreamEvent.EventKind.Unsubscribe;
            }

            public IInstrumentedCall InstrumentedCall => mCall;

            public string MethodName => mCall.CalledMethod;

            public ICommand StartMonitoringCommand { get; }
            public ICommand StopMonitoringCommand { get; }

            private ObservableAsPropertyHelper<string> mActiveSubscriptionCount;
            public string ActiveSubscriptionCount => mActiveSubscriptionCount?.Value;
        }

        public void SetDocument(IMonitoringConfiguration document)
        {
            mDocument = document;
        }
    }
}
