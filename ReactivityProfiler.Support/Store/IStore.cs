namespace ReactivityProfiler.Support.Store
{
    internal interface IStore
    {
        IInstrumentationStore Instrumentation { get; }
        ISubscriptionStore Subscriptions { get; }
        IRxEventStore RxEvents { get; }

        void NotifyObservableCreated(ObservableInfo obs);

        void SinkEvents(IStoreEventSink sink);
        void StartMonitoring(int instrumentationPoint);
        void StopMonitoring(int instrumentationPoint);
    }
}
