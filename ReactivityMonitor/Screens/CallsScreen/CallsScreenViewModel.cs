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
using System.Collections.Immutable;

namespace ReactivityMonitor.Screens.CallsScreen
{
    public sealed class CallsScreenViewModel : ReactiveViewModel, ICallsScreen
    {
        public string DisplayName => "Instrumented methods";

        public CallsScreenViewModel(IConcurrencyService concurrencyService)
        {
            var modules = new ObservableCollectionExtended<ModuleItem>();
            Modules = new ReadOnlyObservableCollection<ModuleItem>(modules);

            this.WhenActivated(disposables =>
            {
                modules.Clear();

                Workspace.Model.Modules
                    .ToObservableChangeSet(m => m.ModuleId)
                    .Transform(module =>
                    {
                        var callingMethods = module.InstrumentedMethods
                            .Select(method => new CallingMethod(method))
                            .ToObservableChangeSet()
                            .Sort(SortExpressionComparer<CallingMethod>.Ascending(x => (x.TypeName, x.Name)))
                            .ObserveOn(concurrencyService.DispatcherRxScheduler);

                        return new ModuleItem(module.Path, module.AssemblyName, callingMethods);
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
            public CallingMethod(IInstrumentedMethod method)
            {
                TypeName = method.ParentType;
                Name = method.Name;
                Calls = method.InstrumentedCalls.Select(c => new Call(c)).ToImmutableList();
            }

            public string TypeName { get; }

            public string Name { get; }

            public IImmutableList<Call> Calls { get; set; }

            public void Dispose()
            {
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
