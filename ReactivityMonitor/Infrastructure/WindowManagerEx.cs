using Caliburn.Micro;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ReactivityMonitor.Infrastructure
{
    internal sealed class WindowManagerEx : WindowManager
    {
        private readonly ICommandHandlerService mCommandHandlerService;

        public WindowManagerEx(ICommandHandlerService commandHandlerService)
        {
            mCommandHandlerService = commandHandlerService;
        }

        protected override Window CreateWindow(object rootModel, bool isDialog, object context, IDictionary<string, object> settings)
        {
            var window = base.CreateWindow(rootModel, isDialog, context, settings);
            window.AddHandler(CommandManager.CanExecuteEvent, new CanExecuteRoutedEventHandler(mCommandHandlerService.HandleCanExecute));
            window.AddHandler(CommandManager.ExecutedEvent, new ExecutedRoutedEventHandler(mCommandHandlerService.HandleExecuted));

            return window;
        }
    }
}
