using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public sealed class ConcurrencyService : IConcurrencyService
    {
        public ConcurrencyService()
        {
            DispatcherRxScheduler = System.Reactive.Concurrency.DispatcherScheduler.Current;
            TaskPoolRxScheduler = System.Reactive.Concurrency.TaskPoolScheduler.Default;
        }

        public IScheduler DispatcherRxScheduler { get; }

        public IScheduler TaskPoolRxScheduler { get; }
    }
}
