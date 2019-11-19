using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support
{
    public static class Instrument
    {
        public static IObservable<T> Returned<T>(IObservable<T> observable, int instrumentationPoint)
        {
            return observable;
        }
    }
}
