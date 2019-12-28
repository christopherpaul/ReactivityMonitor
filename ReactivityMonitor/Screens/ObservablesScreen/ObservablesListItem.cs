using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public sealed class ObservablesListItem : ReactiveViewModel
    {
        public ObservablesListItem(IConcurrencyService concurrencyService)
        {
        }

        public IObservableInstance ObservableInstance { get; set; }

        public long SequenceId => ObservableInstance.Created.SequenceId;
        public DateTime Timestamp => ObservableInstance.Created.Timestamp;
        public long ThreadId => ObservableInstance.Created.ThreadId;
    }
}
