using ReactivityProfiler.Support.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            private readonly List<ObservableInfo> mInfoList = new List<ObservableInfo>();
            private readonly Stack<(int InstrPoint, IReadOnlyList<ObservableInfo> Inputs)> mCallStack = new Stack<(int InstrPoint, IReadOnlyList<ObservableInfo> Inputs)>();
            private int mCurrentInstrPoint;

            public void AddInfo(int instrumentationPoint, ObservableInfo obsInfo)
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

            public IReadOnlyList<ObservableInfo> Returned(int instrumentationPoint)
            {
                (int InstrPoint, IReadOnlyList<ObservableInfo> Inputs) entry;
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

        /// <summary>
        /// Called for each IObservable argument of an instrumented method call.
        /// </summary>
        public static IObservable<T> Argument<T>(IObservable<T> observable, int instrumentationPoint)
        {
            if (observable is InstrumentedObservable<T> instrumented)
            {
                Trace.WriteLine($"{instrumentationPoint}: Argument({instrumented.Info.ObservableId})");
                sTracker.Value.AddInfo(instrumentationPoint, instrumented.Info);
            }
            else
            {
                Trace.WriteLine($"{instrumentationPoint}: Argument(?)");
            }
            return observable;
        }

        /// <summary>
        /// Called just before an instrumented method call (after any <see cref="Argument"/> calls).
        /// </summary>
        public static void Calling(int instrumentationPoint)
        {
            Trace.WriteLine($"{instrumentationPoint}: Calling");
            sTracker.Value.Calling(instrumentationPoint);
        }

        /// <summary>
        /// Called after an instrumented method call returns.
        /// </summary>
        public static IObservable<T> Returned<T>(IObservable<T> observable, int instrumentationPoint)
        {
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

            var inputs = sTracker.Value.Returned(instrumentationPoint);
            var obsInfo = new ObservableInfo(instrumentationPoint, inputs);
            Trace.WriteLine($"{instrumentationPoint}: Returned({obsInfo.ObservableId} <- {string.Join(", ", inputs.Select(x => x.ObservableId))})");

            Services.Store.NotifyObservableCreated(obsInfo);
            return new InstrumentedObservable<T>(observable, obsInfo);
        }
    }
}
