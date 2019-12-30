using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Definitions
{
    public static class Commands
    {
        public static ICommand Go { get; } = new RoutedCommand(nameof(Go), typeof(Commands));
        public static ICommand Pause { get; } = new RoutedCommand(nameof(Pause), typeof(Commands));
        public static ICommand ClearEventList { get; } = new RoutedCommand(nameof(ClearEventList), typeof(Commands));
        public static ICommand FilterEventList { get; } = new RoutedCommand(nameof(FilterEventList), typeof(Commands));
    }
}
