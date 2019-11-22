using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support
{
    internal sealed class TracingObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> mObservable;
        private readonly int mObservableId;
        private int mSubscriptionId;

        public TracingObservable(IObservable<T> observable, int observableId)
        {
            mObservable = observable;
            mObservableId = observableId;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var tracingObserver = new Observer(observer, mObservableId, Interlocked.Increment(ref mSubscriptionId));
            var subscription = mObservable.Subscribe(tracingObserver);
            return new Disposable(subscription, tracingObserver);
        }

        private sealed class Disposable : IDisposable
        {
            private IDisposable mSubscription;
            private Observer mTracingObserver;

            public Disposable(IDisposable subscription, Observer tracingObserver)
            {
                mSubscription = subscription;
                mTracingObserver = tracingObserver;
            }

            public void Dispose()
            {
                mTracingObserver?.OnUnsubscribe();
                mSubscription?.Dispose();
                mTracingObserver = null;
                mSubscription = null;
            }
        }

        private sealed class Observer : IObserver<T>
        {
            private IObserver<T> mObserver;
            private readonly int mObservableId;
            private readonly int mSubscriptionId;

            public Observer(IObserver<T> observer, int observableId, int subscriptionId)
            {
                mObserver = observer;
                mObservableId = observableId;
                mSubscriptionId = subscriptionId;
                Trace("Subscribe");
            }

            public void OnUnsubscribe()
            {
                Trace("Unsubscribe");
                mObserver = null;
            }

            public void OnCompleted()
            {
                Trace("OnCompleted");
                mObserver.OnCompleted();
            }

            public void OnError(Exception error)
            {
                Trace($"OnError({error.Message})");
                mObserver.OnError(error);
            }

            public void OnNext(T value)
            {
                Trace($"OnNext({value})");
                mObserver.OnNext(value);
            }

            private void Trace(string message)
            {
                System.Diagnostics.Trace.WriteLine($"Obs{mObservableId}:Sub{mSubscriptionId}:{message}");
            }
        }
    }
}
