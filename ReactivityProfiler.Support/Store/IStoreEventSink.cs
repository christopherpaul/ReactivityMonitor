using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal interface IStoreEventSink
    {
        void ObservableCreated(ObservableInfo obs);
        void ObservablesLinked(ObservableInfo output, ObservableInfo input);
        void Subscribed(SubscriptionInfo sub);
        void Unsubscribed(ref CommonEventDetails details, SubscriptionInfo sub);
        void OnNext<T>(ref CommonEventDetails details, SubscriptionInfo sub, T value);
        void OnCompleted(ref CommonEventDetails details, SubscriptionInfo sub);
        void OnError(ref CommonEventDetails details, SubscriptionInfo sub, Exception error);
    }
}
