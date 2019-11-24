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

        /// <summary>
        /// Called for each IObservable argument of an instrumented method call.
        /// </summary>
        public static IObservable<T> Argument<T>(IObservable<T> observable, int instrumentationPoint)
        {
            Trace.WriteLine($"Argument(X, {instrumentationPoint})");
            return observable;
        }

        /// <summary>
        /// Called just before an instrumented method call (after any <see cref="Argument"/> calls).
        /// </summary>
        public static void Calling(int instrumentationPoint)
        {
            Trace.WriteLine($"Calling({instrumentationPoint})");
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

            var obsInfo = new ObservableInfo(instrumentationPoint, new ObservableInfo[0]);
            return new InstrumentedObservable<T>(observable, obsInfo);
        }
    }
}
