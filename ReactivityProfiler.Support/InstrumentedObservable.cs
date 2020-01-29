using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ReactivityProfiler.Support.Store;

namespace ReactivityProfiler.Support
{
    /// <summary>
    /// Needs to be public so that dynamic proxy type can access.
    /// </summary>
    public interface IInstrumentedObservable
    {
        /// <summary>
        /// Always of type <see cref="ObservableInfo"/>, but want to keep that type internal.
        /// </summary>
        object Info { get; }
    }

    /// <summary>
    /// Needs to be public so that dynamic proxy type can access.
    /// </summary>
    public abstract class InstrumentedObservableProxy : IInstrumentedObservable
    {
        private readonly IInstrumentedObservable mInstrumentedObservable;

        public InstrumentedObservableProxy(object instrumentedObservable)
        {
            mInstrumentedObservable = (IInstrumentedObservable)instrumentedObservable;
        }

        public object Info => mInstrumentedObservable.Info;
    }

    internal sealed class InstrumentedObservable<T> : IObservable<T>, IInstrumentedObservable
    {
        private readonly IObservable<T> mObservable;

        public InstrumentedObservable(IObservable<T> observable, ObservableInfo info)
        {
            mObservable = observable;
            Info = info;
        }

        public ObservableInfo Info { get; }
        object IInstrumentedObservable.Info => Info;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var sub = Services.Store.Subscriptions.CreateSub(Info);
            var instrumentedObserver = new Observer(observer, sub);
            var subscription = mObservable.Subscribe(instrumentedObserver);
            return new Disposable(subscription, instrumentedObserver);
        }

        private sealed class Disposable : IDisposable
        {
            private IDisposable mSubscription;
            private Observer mInstrumentedObserver;

            public Disposable(IDisposable subscription, Observer instrumentedObserver)
            {
                mSubscription = subscription;
                mInstrumentedObserver = instrumentedObserver;
            }

            public void Dispose()
            {
                mInstrumentedObserver?.OnUnsubscribe();
                mSubscription?.Dispose();
                mInstrumentedObserver = null;
                mSubscription = null;
            }
        }

        private sealed class Observer : IObserver<T>
        {
            private IObserver<T> mObserver;
            private readonly SubscriptionInfo mSubscriptionInfo;

            public Observer(IObserver<T> observer, SubscriptionInfo subscriptionInfo)
            {
                mObserver = observer;
                mSubscriptionInfo = subscriptionInfo;
            }

            public void OnUnsubscribe()
            {
                try
                {
                    Services.Store.Subscriptions.Unsubscribed(mSubscriptionInfo);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("{0}<{1}> threw an exception: {2}", nameof(OnUnsubscribe), typeof(T).FullName, ex);
                }
                mObserver = null;
            }

            public void OnCompleted()
            {
                try
                {
                    Services.Store.RxEvents.AddOnCompleted(mSubscriptionInfo);
                    Services.Store.Subscriptions.Terminated(mSubscriptionInfo);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("{0}<{1}> threw an exception: {2}", nameof(OnCompleted), typeof(T).FullName, ex);
                }

                mObserver.OnCompleted();
            }

            public void OnError(Exception error)
            {
                try
                {
                    Services.Store.RxEvents.AddOnError(mSubscriptionInfo, error);
                    Services.Store.Subscriptions.Terminated(mSubscriptionInfo);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("{0}<{1}> threw an exception: {2}", nameof(OnError), typeof(T).FullName, ex);
                }

                mObserver.OnError(error);
            }

            public void OnNext(T value)
            {
                try
                {
                    Services.Store.RxEvents.AddOnNext(mSubscriptionInfo, value);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("{0}<{1}> threw an exception: {2}", nameof(OnNext), typeof(T).FullName, ex);
                }

                mObserver.OnNext(value);
            }
        }
    }
}
