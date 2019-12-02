using ReactivityProfiler.Support.Store;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support
{
    internal interface IObservableInput
    {
        void AssociateWith(ObservableInfo info);
    }
}
