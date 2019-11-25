using System;

namespace ReactivityProfiler.Support.Store
{
    internal interface IRxEventStore
    {
        void AddOnCompleted(long subscriptionId);
        void AddOnError(long subscriptionId, Exception e);
        void AddOnNext<T>(long subscriptionId, T value);
    }
}