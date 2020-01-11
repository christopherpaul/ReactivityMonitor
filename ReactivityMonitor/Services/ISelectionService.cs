using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public interface ISelectionService
    {
        IObservable<Selection> WhenSelectionChanges { get; }

        void ChangeSelection(Func<Selection, Selection> changer);
    }
}
