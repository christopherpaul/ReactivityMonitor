using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support
{
    public static class Instrument
    {
        private static int sObservableId;

        /// <summary>
        /// Called for each IObservable argument of an instrumented method call.
        /// </summary>
        public static IObservable<T> Argument<T>(IObservable<T> observable, int instrumentationPoint)
        {
            return observable;
        }

        /// <summary>
        /// Called just before an instrumented method call (after any <see cref="Argument"/> calls).
        /// </summary>
        public static void Calling(int instrumentationPoint)
        {
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

            int observableId = Interlocked.Increment(ref sObservableId);
            Trace.WriteLine($"InstrPoint{instrumentationPoint}:Obs{observableId}");
            return new TracingObservable<T>(observable, observableId);
        }
    }
}
