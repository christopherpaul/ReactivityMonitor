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

        public static IObservable<T> Returned<T>(IObservable<T> observable, int instrumentationPoint)
        {
            int observableId = Interlocked.Increment(ref sObservableId);
            Trace.WriteLine($"InstrPoint{instrumentationPoint}:Obs{observableId}");
            return new TracingObservable<T>(observable, observableId);
        }
    }
}
