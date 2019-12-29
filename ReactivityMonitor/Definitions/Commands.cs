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
        public static ICommand Go { get; } = new RoutedCommand("Go", typeof(Commands));
        public static ICommand Pause { get; } = new RoutedCommand("Pause", typeof(Commands));
    }
}
