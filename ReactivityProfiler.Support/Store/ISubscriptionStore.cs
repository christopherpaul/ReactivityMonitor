﻿using System.Collections.Generic;

namespace ReactivityProfiler.Support.Store
{
    internal interface ISubscriptionStore
    {
        long CreateSub(ObservableInfo observable);
        void DeleteSub(long subId);
        IEnumerable<SubscriptionInfo> GetAllSubs();
        SubscriptionInfo GetSub(long subId);
    }
}