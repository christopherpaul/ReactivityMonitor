using System.Collections.Generic;

namespace ReactivityProfiler.Support.Store
{
    internal interface ISubscriptionStore
    {
        SubscriptionInfo CreateSub(ObservableInfo observable);
        void Unsubscribed(SubscriptionInfo sub);
        void Terminated(SubscriptionInfo sub);
        IEnumerable<SubscriptionInfo> GetAllSubs();
        SubscriptionInfo GetSub(long subId);
        IEnumerable<SubscriptionInfo> GetSubs(int instrumentationPointId);
        IEnumerable<SubscriptionInfo> GetSubs(ObservableInfo obs);
    }
}