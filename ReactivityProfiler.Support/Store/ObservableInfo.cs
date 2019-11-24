using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Store
{
    /// <summary>
    /// Holds information about an IObservable instance.
    /// </summary>
    internal sealed class ObservableInfo
    {
        private static long sObservableIdSource;

        public ObservableInfo(int instrumentationPoint, IReadOnlyList<ObservableInfo> inputs)
        {
            ObservableId = Interlocked.Increment(ref sObservableIdSource);
            Details = CommonEventDetails.Capture();
            InstrumentationPoint = instrumentationPoint;
            Inputs = inputs;
        }

        public long ObservableId { get; }
        public int InstrumentationPoint { get; }
        public IReadOnlyList<ObservableInfo> Inputs { get; }
        public CommonEventDetails Details { get; }
    }
}
