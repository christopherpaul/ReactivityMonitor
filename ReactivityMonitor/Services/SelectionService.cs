using ReactiveUI;
using ReactivityMonitor.Definitions;
using ReactivityMonitor.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    internal sealed class SelectionService : ISelectionService
    {
        private readonly Action<Func<Selection, Selection>> mMakeChange;

        public SelectionService(IConcurrencyService concurrencyService, ICommandHandlerService commandHandlerService)
        {
            var changes = Subject.Synchronize(new Subject<Func<Selection, Selection>>());
            mMakeChange = changes.OnNext;

            WhenSelectionChanges = changes
                .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                .Scan(Selection.Empty, (sel, change) => change(sel))
                .Publish(Selection.Empty)
                .ConnectForEver();

            commandHandlerService.RegisterHandler(
                Commands.ChangeSelectedEventItems, 
                ReactiveCommand.Create<Func<Selection, Selection>>(ChangeSelection));
        }

        public IObservable<Selection> WhenSelectionChanges { get; }

        public void ChangeSelection(Func<Selection, Selection> changer)
        {
            if (changer is null)
            {
                throw new ArgumentNullException(nameof(changer));
            }

            mMakeChange(changer);
        }
    }
}
