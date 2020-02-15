using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public interface IDialogViewModel<T>
    {
        Action<T> Proceed { get; set; }
        Action Cancel { get; set; }
    }
}
