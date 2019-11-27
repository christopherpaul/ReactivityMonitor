using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.CallsScreen
{
    public interface ICallingMethod
    {
        string TypeName { get; }
        string Name { get; }
        ReadOnlyObservableCollection<ICall> Calls { get; }
    }
}
