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

        public ObservableInfo CreateObservable(int instrumentationPoint)
        {
            var obs = new ObservableInfo(instrumentationPoint);
            mEventMediator.ObservableCreated(obs);
            return obs;
        }

        public void NotifyObservablesLinked(ObservableInfo output, ObservableInfo input)
        {
            mEventMediator.ObservablesLinked(output, input);
        }

        public void NotifyObservablesUnlinked(ObservableInfo output, ObservableInfo input)
        {
            // No need to do anything?
        }

        public void SinkEvents(IStoreEventSink sink) => mEventMediator.SinkEvents(sink);

        public void StartMonitoring(int instrumentationPoint) => mEventMediator.StartMonitoring(instrumentationPoint);

        public void StopMonitoring(int instrumentationPoint) => mEventMediator.StopMonitoring(instrumentationPoint);

        public void StartMonitoringAll() => mEventMediator.StartMonitoringAll();

        public void StopMonitoringAll() => mEventMediator.StopMonitoringAll();

        public CommonEventDetails RecordEvent() => CommonEventDetails.Capture();

        private sealed class EventMediator : IStoreEventSink
        {
            private IStoreEventSink mEventSink;
            private readonly ConcurrentDictionary<int, bool> mMonitoredInstrumentationPoints = new ConcurrentDictionary<int, bool>();
            private bool mIsMonitoringAll;

            public ISubscriptionStore Subscriptions { get; set; }

            public void SinkEvents(IStoreEventSink sink)
            {
                mEventSink = sink;
            }

            public void StartMonitoring(int instrumentationPoint)
            {
                if (mMonitoredInstrumentationPoints.TryAdd(instrumentationPoint, true))
                {
                    foreach (var sub in Subscriptions.GetSubs(instrumentationPoint))
                    {
                        MonitorChain(sub.Observable);
                    }
                }
            }

            public void StopMonitoring(int instrumentationPoint)
            {
                if (mMonitoredInstrumentationPoints.TryRemove(instrumentationPoint, out _))
                {
                    foreach (var sub in Subscriptions.GetSubs(instrumentationPoint))
                    {
                        UnmonitorChain(sub.Observable);
                    }
                }
            }

            public void StartMonitoringAll()
            {
                mIsMonitoringAll = true;
                foreach (var sub in Subscriptions.GetAllSubs())
                {
                    MonitorChain(sub.Observable);
                }
            }

            public void StopMonitoringAll()
            {
                mIsMonitoringAll = false;
                mMonitoredInstrumentationPoints.Clear();
                ObservableInfo.StopMonitoringAll();
            }

            public void ObservableCreated(ObservableInfo rootObs)
            {
                if (mIsMonitoringAll || mMonitoredInstrumentationPoints.ContainsKey(rootObs.InstrumentationPoint))
                {
                    MonitorChain(rootObs);
                }
            }

            public void ObservablesLinked(ObservableInfo output, ObservableInfo input)
            {
                if (output.Monitoring)
                {
                    mEventSink?.ObservablesLinked(output, input);
                    MonitorChain(input);
                }
            }

            public void OnCompleted(ref CommonEventDetails details, SubscriptionInfo sub)
            {
                if (IsMonitoringSub(sub))
                {
                    mEventSink?.OnCompleted(ref details, sub);
                }
            }

            public void OnError(ref CommonEventDetails details, SubscriptionInfo sub, Exception error)
            {
                if (IsMonitoringSub(sub))
                {
                    mEventSink?.OnError(ref details, sub, error);
                }
            }

            public void OnNext<T>(ref CommonEventDetails details, SubscriptionInfo sub, T value)
            {
                if (IsMonitoringSub(sub))
                {
                    mEventSink?.OnNext(ref details, sub, value);
                }
            }

            public void Subscribed(SubscriptionInfo sub)
            {
                IsMonitoringSub(sub);
            }

            public void Unsubscribed(ref CommonEventDetails details, SubscriptionInfo sub)
            {
                if (IsMonitoringSub(sub))
                {
                    mEventSink?.Unsubscribed(ref details, sub);
                }
            }

            private bool IsMonitoringSub(SubscriptionInfo sub)
            {
                if (sub.Monitoring)
                {
                    return true;
                }

                if (sub.Observable.Monitoring)
                {
                    sub.Monitoring = true;
                    mEventSink?.Subscribed(sub);
                    return true;
                }

                // if the observable was already created but had no subscriptions when we began monitoring
                // the instrumentation point, it would not have been picked up then, so deal with it
                // retroactively here.
                if (mIsMonitoringAll || mMonitoredInstrumentationPoints.ContainsKey(sub.Observable.InstrumentationPoint))
                {
                    // Note that MonitorChain will invoke this method recursively, but it will
                    // have set sub.Observable.Monitoring = true so will not reach this point.
                    // Because of this, we just need to return true afterwards.
                    MonitorChain(sub.Observable);
                    return true;
                }

                return false;
            }

            private void MonitorChain(ObservableInfo obs)
            {
                if (obs.Monitoring)
                {
                    return;
                }

                obs.Monitoring = true;

                mEventSink?.ObservableCreated(obs);

                foreach (var sub in Subscriptions.GetSubs(obs))
                {
                    Subscribed(sub);
                }

                foreach (var input in obs.Inputs)
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
