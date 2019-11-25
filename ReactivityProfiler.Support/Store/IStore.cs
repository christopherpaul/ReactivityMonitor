namespace ReactivityProfiler.Support.Store
{
    internal interface IStore
    {
        IInstrumentationStore Instrumentation { get; }
        ISubscriptionStore Subscriptions { get; }
        IRxEventStore RxEvents { get; }
    }
}
