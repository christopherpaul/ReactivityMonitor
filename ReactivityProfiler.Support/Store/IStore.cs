namespace ReactivityProfiler.Support.Store
{
    internal interface IStore
    {
        IInstrumentationStore Instrumentation { get; }
        ISubscriptionStore Subscriptions { get; }
        IRxEventStore RxEvents { get; }

        ObservableInfo CreateObservable(int instrumentationPoint);
        void NotifyObservablesLinked(ObservableInfo output, ObservableInfo input);
        void NotifyObservablesUnlinked(ObservableInfo output, ObservableInfo input);

        void SinkEvents(IStoreEventSink sink);
        void StartMonitoring(int instrumentationPoint);
        void StopMonitoring(int instrumentationPoint);
        void StopMonitoringAll();
    }
}
