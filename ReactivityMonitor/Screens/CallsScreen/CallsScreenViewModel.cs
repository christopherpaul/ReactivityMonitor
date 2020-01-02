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
            var modules = new ObservableCollectionExtended<ModuleItem>();
            Modules = new ReadOnlyObservableCollection<ModuleItem>(modules);

            WhenActivated(disposables =>
            {
                modules.Clear();

                Model.InstrumentedCalls
                    .GroupBy(ic => ic.Module)
                    .ToObservableChangeSet(m => m.Key.ModuleId)
                    .Transform(moduleCalls =>
                    {
                        var callingMethods = moduleCalls
                            .GroupBy(ic => (ic.CallingType, ic.CallingMethod))
                            .Select(grp =>
                            {
                                var callsChanges = grp
                                    .ToObservableChangeSet(ic => ic.InstrumentedCallId)
                                    .Transform(ic => new Call(ic))
                                    .Sort(SortExpressionComparer<Call>.Ascending(ic => ic.InstructionOffset))
                                    .ObserveOn(concurrencyService.DispatcherRxScheduler);

                                var callingMethod = new CallingMethod(grp.Key.Item1, grp.Key.Item2, callsChanges);
                                return callingMethod;
                            })
                            .ToObservableChangeSet()
                            .Sort(SortExpressionComparer<CallingMethod>.Ascending(x => (x.TypeName, x.Name)))
                            .ObserveOn(concurrencyService.DispatcherRxScheduler);

                        return new ModuleItem(moduleCalls.Key.Path, moduleCalls.Key.AssemblyName, callingMethods);
                    })
                    .Sort(SortExpressionComparer<ModuleItem>.Ascending(x => x.AssemblyName))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(modules)
                    .DisposeMany()
                    .Subscribe()
                    .DisposeWith(disposables);
            });

            MonitorCall = ReactiveCommand.Create((Call c) =>
            {
                var monitoredCall = Workspace.StartMonitoringCall(c.InstrumentedCall);
                var monitoringGroup = Workspace.CreateMonitoringGroup(c.CalledMethodName);
                monitoringGroup.AddCall(monitoredCall);
            });
        }

        public IReactivityModel Model { get; set; }
        public IWorkspace Workspace { get; set; }

        public ReadOnlyObservableCollection<ModuleItem> Modules { get; }

        public ICommand MonitorCall { get; }

        public sealed class ModuleItem : IDisposable
        {
            private readonly ObservableCollectionExtended<CallingMethod> mCallingMethods;
            private readonly IDisposable mDisposable;

            public ModuleItem(string path, string assemblyName, IObservable<IChangeSet<CallingMethod>> callingMethodsSource)
            {
                mCallingMethods = new ObservableCollectionExtended<CallingMethod>();
                CallingMethods = new ReadOnlyObservableCollection<CallingMethod>(mCallingMethods);

                mDisposable = callingMethodsSource
                    .Bind(mCallingMethods)
                    .DisposeMany()
                    .Subscribe();

                Path = path;
                AssemblyName = string.IsNullOrEmpty(assemblyName) ? path : assemblyName;
            }

            public string Path { get; }
            public string AssemblyName { get; }

            public ReadOnlyObservableCollection<CallingMethod> CallingMethods { get; private set; }

            public void Dispose()
            {
                mDisposable.Dispose();
            }
        }

        public sealed class CallingMethod : IDisposable
        {
            private readonly IDisposable mDisposable;

            public CallingMethod(string typeName, string methodName, IObservable<IChangeSet<Call, int>> callsChanges)
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

            public ReadOnlyObservableCollection<Call> Calls { get; set; }

            public void Dispose()
            {
                mDisposable.Dispose();
            }
        }

        public sealed class Call
        {
            public Call(IInstrumentedCall call)
            {
                InstrumentedCall = call;
            }

            public IInstrumentedCall InstrumentedCall { get; }
            public string CalledMethodName => InstrumentedCall.CalledMethod;

            public int InstructionOffset => InstrumentedCall.InstructionOffset;
        }
    }
}
