using Microsoft.Reactive.Testing;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Text;

namespace ReactivityMonitor.Utility.Tests
{
    /// <summary>
    /// Hacky way to get TestScheduler to behave sensibly (i.e. not prohibit the scheduling of actions at the
    /// current time).
    /// </summary>
    internal class PredictableTestScheduler : TestScheduler
    {
        public override IDisposable ScheduleAbsolute<TState>(TState state, long dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            var savedClock = Clock;
            try
            {
                Clock = long.MinValue;
                return base.ScheduleAbsolute(state, dueTime, action);
            }
            finally
            {
                Clock = savedClock;
            }
        }
    }
}
