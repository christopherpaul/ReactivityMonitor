using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public interface IDialogService
    {
        Task ShowErrorDialog(string title, string message);

        Task<string> ShowOpenFileDialog(
            string title,
            string filter,
            string initialDirectory = null);

        Task<T> ShowDialogContent<T>(IDialogViewModel<T> viewModel);
    }
}
