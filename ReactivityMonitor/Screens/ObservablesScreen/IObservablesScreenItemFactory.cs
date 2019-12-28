using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public interface IObservablesScreenItemFactory
    {
        ObservablesListItem CreateItem(IObservableInstance obs);
    }
}
