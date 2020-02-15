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

namespace ReactivityMonitor.Screens.MonitoringConfigurationScreen
{
    public sealed class MonitoringConfigurationScreenViewModel : ReactiveViewModel, IMonitoringConfigurationScreen
    {
        public string DisplayName => "Configuration";

        public MonitoringConfigurationScreenViewModel(IConcurrencyService concurrencyService)
        {
            var methodItems = new ObservableCollectionExtended<MethodItem>();
            Methods = new ReadOnlyObservableCollection<MethodItem>(methodItems);

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                var monitoredCalls = Workspace.MonitoredCalls
                    .TakeUntilDisposed(disposables)
                    .Transform(c => c.Call)
                    .AddKey(c => c.InstrumentedCallId)
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .AsObservableCache();

                Workspace.Methods
                    .Transform(method => new MethodItem(method, monitoredCalls, concurrencyService))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(methodItems)
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<MethodItem> Methods { get; }

        public sealed class MethodItem : IDisposable
        {
            private readonly CompositeDisposable mDisposables;
            private readonly IInstrumentedMethod mMethod;

            public MethodItem(IInstrumentedMethod method, IObservableCache<IInstrumentedCall, int> monitoredCalls, IConcurrencyService concurrencyService)
            {
                mDisposables = new CompositeDisposable();

                Calls = method.InstrumentedCalls
                    .Select(CreateCallItem)
                    .ToImmutableList();

                CallItem CreateCallItem(IInstrumentedCall c)
                {
                    var whenIsMonitoredChanges = monitoredCalls.Watch(c.InstrumentedCallId)
                        .TakeUntilDisposed(mDisposables)
                        .Select(chg => chg.Reason != ChangeReason.Remove)
                        .DistinctUntilChanged()
                        .ObserveOn(concurrencyService.DispatcherRxScheduler);

                    return new CallItem(c, whenIsMonitoredChanges);
                }

                mMethod = method;
            }

            public string TypeName => mMethod.ParentType;
            public string MethodName => mMethod.Name;

            public IImmutableList<CallItem> Calls { get; }

            public void Dispose()
            {
                mDisposables.Dispose();
            }
        }

        public sealed class CallItem : ReactiveObject
        {
            private readonly IInstrumentedCall mCall;

            public CallItem(IInstrumentedCall call, IObservable<bool> whenIsMonitoredChanges)
            {
                mCall = call;
                mIsMonitored = whenIsMonitoredChanges.ToProperty(this, x => x.IsMonitored);
            }

            public string MethodName => mCall.CalledMethod;

            private readonly ObservableAsPropertyHelper<bool> mIsMonitored;
            public bool IsMonitored => mIsMonitored.Value;
        }
    }
}
