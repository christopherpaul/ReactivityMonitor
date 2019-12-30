using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Services
{
    public interface ICommandHandlerService
    {
        IDisposable RegisterHandler(ICommand commandToHandle, ICommand handler);
        void HandleCanExecute(object sender, CanExecuteRoutedEventArgs e);
        void HandleExecuted(object sender, ExecutedRoutedEventArgs e);
    }
}
