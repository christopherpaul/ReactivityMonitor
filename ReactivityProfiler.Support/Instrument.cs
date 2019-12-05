using ReactivityProfiler.Support.Store;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support
{
    public static class Instrument
    {
        // Class constructor gives us an opportunity to fire up our server
        static Instrument()
        {
            try
            {
                var server = new Server.Server(Services.Store);
                server.Start();
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0}: failed to launch server: {1}", typeof(Instrument).FullName, ex);
            }
        }

        /// <summary>
        /// Per-thread tracking of instrumentation calls.
        /// </summary>
        private sealed class CallTracker
        {
            private readonly List<IObservableInput> mInfoList = new List<IObservableInput>();
            private readonly Stack<(int InstrPoint, IReadOnlyList<IObservableInput> Inputs)> mCallStack = new Stack<(int InstrPoint, IReadOnlyList<IObservableInput> Inputs)>();
            private int mCurrentInstrPoint;

            public void AddInput(int instrumentationPoint, IObservableInput obsInfo)
            {
                if (instrumentationPoint != mCurrentInstrPoint)
                {
                    mInfoList.Clear();
                    mCurrentInstrPoint = instrumentationPoint;
                }
                mInfoList.Add(obsInfo);
            }

            public void Calling(int instrumentationPoint)
            {
                if (instrumentationPoint != mCurrentInstrPoint)
                {
                    mInfoList.Clear();
                }
                mCallStack.Push((instrumentationPoint, mInfoList.ToArray()));
                mInfoList.Clear();
            }

            public IReadOnlyList<IObservableInput> Returned(int instrumentationPoint)
            {
                (int InstrPoint, IReadOnlyList<IObservableInput> Inputs) entry;
                do
                {
                    if (mCallStack.Count == 0)
                    {
                        return new ObservableInfo[0];
                    }

                    entry = mCallStack.Pop();
                }
                while (entry.InstrPoint != instrumentationPoint);

                return entry.Inputs;
            }
        }

        private static ThreadLocal<CallTracker> sTracker = new ThreadLocal<CallTracker>(() => new CallTracker());

        private static class ArgumentTypeSpecialisation<T>
        {
            public static readonly Func<T, int, T> ArgHandler;

            static ArgumentTypeSpecialisation()
            {
                var argType = typeof(T);

                // Check for T = IObservable<U>
                if (IsObservable(argType, out Type observableItemType))
                {
                    var handlerType = typeof(ObservableArgHandler<>).MakeGenericType(observableItemType);
                    ArgHandler = (Func<T, int, T>)((IObservableArgHandler)Activator.CreateInstance(handlerType)).GetHandler();
                }

                // Check for T = delegate returning IObservable<U>
                if (typeof(Delegate).IsAssignableFrom(argType))
                {
                    var invokeMethod = argType.GetMethod("Invoke");
                    var returnType = invokeMethod.ReturnType;
                    if (IsObservable(returnType, out observableItemType))
                    {
                        var attacherType = typeof(DynamicObservableAttacher<>).MakeGenericType(observableItemType);
                        var delegateArgParams = invokeMethod.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
                        var handlerArgParam = Expression.Parameter(argType);
                        var handlerIpParam = Expression.Parameter(typeof(int));
                        var attacherVar = Expression.Parameter(attacherType);
                        var observableVar = Expression.Parameter(returnType);
                        var handlerExpression =
                            Expression.Lambda<Func<T, int, T>>(
                                Expression.Block(new[] {attacherVar},
                                    Expression.Assign(attacherVar, Expression.New(attacherType)),
                                    Expression.Call(
                                        Expression.Property(Expression.Constant(sTracker), "Value"),
                                        typeof(CallTracker).GetMethod("AddInput"),
                                        handlerIpParam, attacherVar),
                                    Expression.Lambda(argType,
                                        Expression.Block(new[] {observableVar},
                                            Expression.Assign(observableVar,
                                                Expression.Invoke(handlerArgParam, delegateArgParams)),
                                            Expression.Call(
                                                attacherVar,
                                                attacherType.GetMethod("Attach"),
                                                observableVar)),
                                        delegateArgParams)),
                                handlerArgParam, handlerIpParam);
                        ArgHandler = handlerExpression.Compile();
                    }
                }
            }

            private static bool IsObservable(Type argType, out Type observableItemType)
            {
                if (argType.IsGenericType && argType.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    observableItemType = argType.GenericTypeArguments[0];
                    return true;
                }
                observableItemType = null;
                return false;
            }
        }

        private interface IObservableArgHandler
        {
            object GetHandler();
        }

        private sealed class ObservableArgHandler<T> : IObservableArgHandler
        {
            public object GetHandler() => new Func<IObservable<T>, int, IObservable<T>>(HandleArg);

            public IObservable<T> HandleArg(IObservable<T> observable, int instrumentationPoint)
            {
                if (observable is InstrumentedObservable<T> instrumented)
                {
                    sTracker.Value.AddInput(instrumentationPoint, instrumented.Info);
                }
                return observable;
            }
        }


        /// <summary>
        /// Called for each IObservable argument of an instrumented method call.
        /// </summary>
        public static T Argument<T>(T argValue, int instrumentationPoint)
        {
            var handler = ArgumentTypeSpecialisation<T>.ArgHandler;
            if (handler == null)
            {
                return argValue;
            }

            return handler(argValue, instrumentationPoint);
        }

        /// <summary>
        /// Called just before an instrumented method call (after any <see cref="Argument"/> calls).
        /// </summary>
        public static void Calling(int instrumentationPoint)
        {
            sTracker.Value.Calling(instrumentationPoint);
        }

        /// <summary>
        /// Called after an instrumented method call returns.
        /// </summary>
        public static IObservable<T> Returned<T, TObs>(IObservable<T> observable, int instrumentationPoint)
        {
            var inputs = sTracker.Value.Returned(instrumentationPoint);

            if (observable == null)
            {
                return observable;
            }

            if (observable is InstrumentedObservable<T> instrObs)
            {
                // Already instrumented. Not sure if we'd want to associate it with any
                // inputs to this call as well - for now assume not.
                return observable;
            }

            if (typeof(TObs) != typeof(IObservable<T>) && typeof(TObs) != typeof(IConnectableObservable<T>))
            {
                return observable;
            }

            var obsInfo = Services.Store.CreateObservable(instrumentationPoint);
            foreach (var input in inputs)
            {
                input.AssociateWith(obsInfo);
            }

            return new InstrumentedObservable<T>(observable, obsInfo);
        }
    }
}
