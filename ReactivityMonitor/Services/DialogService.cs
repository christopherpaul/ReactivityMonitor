using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReactivityMonitor.Services
{
    public sealed class DialogService : IDialogService
    {
        private readonly IConcurrencyService mConcurrencyService;

        public DialogService(IConcurrencyService concurrencyService)
        {
            mConcurrencyService = concurrencyService;
        }

        public Task ShowErrorDialog(string title, string message)
        {
            return OnDispatcher(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                return Unit.Default;
            });
        }

        private async Task<T> OnDispatcher<T>(Func<T> func)
        {
            return await Observable.FromAsync(() => Task.FromResult(func()))
                .SubscribeOn(mConcurrencyService.DispatcherRxScheduler)
                .ObserveOn(mConcurrencyService.TaskPoolRxScheduler);
        }

        public Task<string> ShowOpenFileDialog(string title, string filter, string initialDirectory = null)
        {
            return OnDispatcher(() =>
            {
                var dialog = new OpenFileDialog();
                dialog.Title = title;
                dialog.Filter = filter;
                if (initialDirectory != null)
                {
                    dialog.InitialDirectory = initialDirectory;
                }

                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;

                bool result = dialog.ShowDialog() == true;
                if (!result)
                {
                    return null;
                }

                return dialog.FileName;
            });
        }
    }
}
