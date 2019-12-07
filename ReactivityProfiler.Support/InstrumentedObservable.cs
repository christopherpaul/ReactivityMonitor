using System;
using System.Collections.Generic;
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
            long subId = Services.Store.Subscriptions.CreateSub(Info);
            var instrumentedObserver = new Observer(observer, subId);
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
            private readonly long mSubscriptionId;

            public Observer(IObserver<T> observer, long subscriptionId)
            {
                mObserver = observer;
                mSubscriptionId = subscriptionId;
            }

            public void OnUnsubscribe()
            {
                Services.Store.Subscriptions.DeleteSub(mSubscriptionId);
                mObserver = null;
            }

            public void OnCompleted()
            {
                Services.Store.RxEvents.AddOnCompleted(mSubscriptionId);
                mObserver.OnCompleted();
            }

            public void OnError(Exception error)
            {
                Services.Store.RxEvents.AddOnError(mSubscriptionId, error);
                mObserver.OnError(error);
            }

            public void OnNext(T value)
            {
                Services.Store.RxEvents.AddOnNext(mSubscriptionId, value);
                mObserver.OnNext(value);
            }
        }
    }
}
