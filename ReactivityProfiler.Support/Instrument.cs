using ReactivityProfiler.Support.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support
{
    public static class Instrument
    {
        static Instrument()
        {
            try
            {
                var launcher = new Server.Launcher();
                launcher.Launch();
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0}: failed to launch server: {1}", typeof(Instrument).FullName, ex);
            }
        }

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
            Trace.WriteLine($"Argument(X, {instrumentationPoint})");
            if (observable is InstrumentedObservable<T> instrumented)
            {
                sTracker.Value.AddInfo(instrumentationPoint, instrumented.Info);
            }
            return observable;
        }

        /// <summary>
        /// Called just before an instrumented method call (after any <see cref="Argument"/> calls).
        /// </summary>
        public static void Calling(int instrumentationPoint)
        {
            Trace.WriteLine($"Calling({instrumentationPoint})");
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

            Trace.WriteLine($"Returned({instrumentationPoint})");

            var inputs = sTracker.Value.Returned(instrumentationPoint);

            var obsInfo = new ObservableInfo(instrumentationPoint, inputs);
            return new InstrumentedObservable<T>(observable, obsInfo);
        }
    }
}
