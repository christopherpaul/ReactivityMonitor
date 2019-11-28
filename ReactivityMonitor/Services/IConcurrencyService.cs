using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public interface IConcurrencyService
    {
        IScheduler DispatcherRxScheduler { get; }
        IScheduler TaskPoolRxScheduler { get; }
    }
}
