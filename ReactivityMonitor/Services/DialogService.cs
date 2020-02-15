using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ReactivityMonitor.Services
{
    public sealed class DialogService : IDialogService
    {
        private readonly IConcurrencyService mConcurrencyService;
        private readonly Action<object> mPushDialogViewModel;
        private Action mCancelActiveDialog;

        public DialogService(IConcurrencyService concurrencyService)
        {
            mConcurrencyService = concurrencyService;

            var dialogViewModelSubject = new BehaviorSubject<object>(null);
            mPushDialogViewModel = dialogViewModelSubject.OnNext;

            WhenDialogViewModelChanges = dialogViewModelSubject.AsObservable();
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

        public Task<T> ShowDialogContent<T>(IDialogViewModel<T> viewModel)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action cancel = () => tcs.TrySetCanceled();
            if (Interlocked.CompareExchange(ref mCancelActiveDialog, cancel, null) != null)
            {
                // Not allowed (for now) - cancel it
                tcs.SetCanceled();
            }
            else
            {
                tcs.Task.ContinueWith(_ =>
                {
                    mCancelActiveDialog = null;
                    mPushDialogViewModel(null);
                }, TaskContinuationOptions.RunContinuationsAsynchronously);
                viewModel.Cancel = cancel;
                viewModel.Proceed = x => tcs.TrySetResult(x);
                mPushDialogViewModel(viewModel);
            }

            return tcs.Task;
        }

        public void CancelActiveDialog()
        {
            mCancelActiveDialog?.Invoke();
        }

        public IObservable<object> WhenDialogViewModelChanges { get; }
    }
}
