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

namespace ReactivityMonitor.Screens.CallsScreen
{
    public sealed class CallsScreenViewModel : ReactiveScreen, ICallsScreen
    {
        public CallsScreenViewModel()
        {
            WhenActivated(disposables =>
            {
                var dispatcherScheduler = DispatcherScheduler.Current;

                Model.InstrumentedCalls.Connect()
                    .Group(ic => (ic.CallingType, ic.CallingMethod))
                    .Sort(SortExpressionComparer<IGroup<IInstrumentedCall, int, (string, string)>>.Ascending(x => x.Key))
                    .Transform(grp =>
                    {
                        var callingMethod = new CallingMethod(grp.Key.Item1, grp.Key.Item2);
                        grp.Cache.Connect()
                            .Sort(SortExpressionComparer<IInstrumentedCall>.Ascending(ic => ic.InstructionOffset))
                            .Transform(ic => (ICall)new Call(ic))
                            .ObserveOn(dispatcherScheduler)
                            .Bind(out var calls)
                            .Subscribe()
                            .DisposeWith(disposables);

                        callingMethod.Calls = calls;

                        return (ICallingMethod)callingMethod;
                    })
                    .ObserveOn(dispatcherScheduler)
                    .Bind(out var callingMethods)
                    .Subscribe()
                    .DisposeWith(disposables);

                CallingMethods = callingMethods;
            });
        }

        public IReactivityModel Model { get; set; }

        public ReadOnlyObservableCollection<ICallingMethod> CallingMethods { get; private set; }


        private sealed class CallingMethod : ICallingMethod
        {
            public CallingMethod(string typeName, string methodName)
            {
                TypeName = typeName;
                Name = methodName;

            }

            public string TypeName { get; }

            public string Name { get; }

            public ReadOnlyObservableCollection<ICall> Calls { get; set; }
        }

        private sealed class Call : ICall
        {
            private readonly IInstrumentedCall mCall;

            public Call(IInstrumentedCall call)
            {
                mCall = call;
            }

            public string CalledMethodName => mCall.CalledMethod;
        }
    }
}
