using Caliburn.Micro;
using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public interface IEventListScreen : IScreen
    {
        IReactivityModel Model { get; set; }
    }
}
