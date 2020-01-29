using System;

namespace ReactivityProfiler.Support.Store
{
    internal interface IRxEventStore
    {
        void AddOnCompleted(SubscriptionInfo sub);
        void AddOnError(SubscriptionInfo sub, Exception e);
        void AddOnNext<T>(SubscriptionInfo sub, T value);
    }
}