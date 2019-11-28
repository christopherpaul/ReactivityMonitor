using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class Store : IStore
    {
        private readonly EventMediator mEventMediator;

        public Store()
        {
            mEventMediator = new EventMediator();
            Instrumentation = new InstrumentationStore();
            Subscriptions = new SubscriptionStore() { EventSink = mEventMediator };
            RxEvents = new RxEventStore(Subscriptions) { EventSink = mEventMediator };
            mEventMediator.Subscriptions = Subscriptions;
        }

        public IInstrumentationStore Instrumentation { get; }
        public ISubscriptionStore Subscriptions { get; }
        public IRxEventStore RxEvents { get; }

        public void NotifyObservableCreated(ObservableInfo obs)
        {
            mEventMediator.ObservableCreated(obs);
        }

        public void SinkEvents(IStoreEventSink sink) => mEventMediator.SinkEvents(sink);

        public void StartMonitoring(int instrumentationPoint) => mEventMediator.StartMonitoring(instrumentationPoint);

        public void StopMonitoring(int instrumentationPoint) => mEventMediator.StopMonitoring(instrumentationPoint);

        private sealed class EventMediator : IStoreEventSink
        {
            private IStoreEventSink mEventSink;
            private readonly ConcurrentDictionary<int, bool> mMonitoredInstrumentationPoints = new ConcurrentDictionary<int, bool>();

            public ISubscriptionStore Subscriptions { get; set; }

            public void SinkEvents(IStoreEventSink sink)
            {
                mEventSink = sink;
            }

            public void StartMonitoring(int instrumentationPoint)
            {
                if (mMonitoredInstrumentationPoints.TryAdd(instrumentationPoint, true))
                {
                    var obses = new HashSet<long>();
                    foreach (var sub in Subscriptions.GetSubs(instrumentationPoint))
                    {
                        if (obses.Add(sub.Observable.ObservableId))
                        {
                            ObservableCreated(sub.Observable);
                        }

                        Subscribed(sub);
                    }
                }
            }

            public void StopMonitoring(int instrumentationPoint)
            {
                if (mMonitoredInstrumentationPoints.TryRemove(instrumentationPoint, out _))
                {
                    var obses = new HashSet<long>();
                    foreach (var sub in Subscriptions.GetSubs(instrumentationPoint))
                    {
                        if (obses.Add(sub.Observable.ObservableId))
                        {
                            UnmonitorChain(sub.Observable);
                        }
                    }
                }
            }

            public void ObservableCreated(ObservableInfo obs)
            {
                if (mMonitoredInstrumentationPoints.ContainsKey(obs.InstrumentationPoint))
                {
                    MonitorChain(obs);
                    mEventSink?.ObservableCreated(obs);
                }
            }

            public void OnCompleted(ref CommonEventDetails details, SubscriptionInfo sub)
            {
                if (sub.Observable.Monitoring)
                {
                    mEventSink?.OnCompleted(ref details, sub);
                }
            }

            public void OnError(ref CommonEventDetails details, SubscriptionInfo sub, Exception error)
            {
                if (sub.Observable.Monitoring)
                {
                    mEventSink?.OnError(ref details, sub, error);
                }
            }

            public void OnNext<T>(ref CommonEventDetails details, SubscriptionInfo sub, T value)
            {
                if (sub.Observable.Monitoring)
                {
                    mEventSink?.OnNext(ref details, sub, value);
                }
            }

            public void Subscribed(SubscriptionInfo sub)
            {
                if (sub.Observable.Monitoring)
                {
                    mEventSink?.Subscribed(sub);
                }
            }

            public void Unsubscribed(ref CommonEventDetails details, SubscriptionInfo sub)
            {
                if (sub.Observable.Monitoring)
                {
                    mEventSink?.Unsubscribed(ref details, sub);
                }
            }

            private void MonitorChain(ObservableInfo observable)
            {
                if (observable.Monitoring)
                {
                    return;
                }

                observable.Monitoring = true;
                foreach (var input in observable.Inputs)
                {
                    MonitorChain(input);
                }
            }

            private void UnmonitorChain(ObservableInfo observable)
            {
                // TODO
            }
        }

    }
}
