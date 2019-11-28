using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using System.Reactive.Linq;
using DynamicData.Binding;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using ReactivityMonitor.Workspace;
using System.Windows.Input;
using ReactivityMonitor.Services;
using ReactiveUI;

namespace ReactivityMonitor.Screens.CallsScreen
{
    public sealed class CallsScreenViewModel : ReactiveScreen, ICallsScreen
    {
        public CallsScreenViewModel(IConcurrencyService concurrencyService)
        {
            WhenActivated(disposables =>
            {
                Model.InstrumentedCalls.Connect()
                    .Group(ic => (ic.CallingType, ic.CallingMethod))
                    .Sort(SortExpressionComparer<IGroup<IInstrumentedCall, int, (string, string)>>.Ascending(x => x.Key))
                    .Transform(grp =>
                    {
                        var callsChanges = grp.Cache.Connect()
                            .Sort(SortExpressionComparer<IInstrumentedCall>.Ascending(ic => ic.InstructionOffset))
                            .Transform(ic => (ICall)new Call(ic))
                            .ObserveOn(concurrencyService.DispatcherRxScheduler);

                        var callingMethod = new CallingMethod(grp.Key.Item1, grp.Key.Item2, callsChanges);
                        return (ICallingMethod)callingMethod;
                    })
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(out var callingMethods)
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);

                CallingMethods = callingMethods;
            });

            MonitorCall = ReactiveCommand.Create((Call c) => Workspace.StartMonitoringCall(c.InstrumentedCall));
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<ICallingMethod> CallingMethods { get; private set; }
        public ICommand MonitorCall { get; }


        private sealed class CallingMethod : ICallingMethod, IDisposable
        {
            private readonly IDisposable mDisposable;

            public CallingMethod(string typeName, string methodName, IObservable<IChangeSet<ICall, int>> callsChanges)
            {
                TypeName = typeName;
                Name = methodName;

                mDisposable = callsChanges
                    .Bind(out var calls)
                    .Subscribe();

                Calls = calls;
            }

            public string TypeName { get; }

            public string Name { get; }

            public ReadOnlyObservableCollection<ICall> Calls { get; set; }

            public void Dispose()
            {
                mDisposable.Dispose();
            }
        }

        private sealed class Call : ICall
        {
            public Call(IInstrumentedCall call)
            {
                InstrumentedCall = call;
            }

            public IInstrumentedCall InstrumentedCall { get; }
            public string CalledMethodName => InstrumentedCall.CalledMethod;
        }
    }
}
