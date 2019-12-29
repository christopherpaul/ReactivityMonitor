using ReactivityMonitor.Definitions;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Utility.Flyweights;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Screens.HomeScreen
{
    internal static class GoPauseControl
    {
        public static IObservable<bool> SetupGoPause(CommandBindingCollection bindings)
        {
            var goTrigger = CommandHelper.CreateTriggerCommand(out var goCommand);
            var pauseTrigger = CommandHelper.CreateTriggerCommand(out var pauseCommand);

            bindings.AddRange(new[] 
            {                 
                CommandHelper.CreateBinding(Commands.Go, goCommand),
                CommandHelper.CreateBinding(Commands.Pause, pauseCommand)
            });

            return Observable.Return(true).Expand(isUpdating => isUpdating
                ? pauseTrigger.Take(1).Select(Funcs<Unit>.False)
                : goTrigger.Take(1).Select(Funcs<Unit>.True));
        }
    }
}
