using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Dialogs.AddMethod
{
    public interface IAddMethodDialog : IDialogViewModel<IInstrumentedMethod>
    {
        IReactivityModel Model { get; set; }
    }
}
