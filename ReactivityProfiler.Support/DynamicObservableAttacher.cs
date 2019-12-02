using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ReactivityProfiler.Support.Store;

namespace ReactivityProfiler.Support
{
    internal abstract class DynamicObservableAttacher : IObservableInput
    {
        protected ObservableInfo Info { get; private set; }

        public void AssociateWith(ObservableInfo info)
        {
            Info = info;
        }
    }

    internal class DynamicObservableAttacher<T> : DynamicObservableAttacher
    {
        public IObservable<T> Attach(IObservable<T> observable)
        {
            if (observable is InstrumentedObservable<T> instrumentedObservable)
            {
                return new AttachingObservable(instrumentedObservable, instrumentedObservable.Info, Info);
            }

            return observable;
        }

        internal sealed class AttachingObservable : IObservable<T>
        {
            private readonly IObservable<T> mObservable;
            private readonly ObservableInfo mInfoToWrap;
            private readonly ObservableInfo mInfoToAttachTo;
            private int mSubCount;
            private object mSync = new object();

            public AttachingObservable(IObservable<T> observable, ObservableInfo infoToWrap, ObservableInfo infoToAttachTo)
            {
                mObservable = observable;
                mInfoToWrap = infoToWrap;
                mInfoToAttachTo = infoToAttachTo;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                lock (mSync)
                {
                    mSubCount++;
                    if (mSubCount == 1)
                    {
                        mInfoToAttachTo.AddInput(mInfoToWrap);
                    }
                }

                var subscription = mObservable.Subscribe(observer);
                return new Disposable(subscription, this);
            }

            private void OnUnsubscribe()
            {
                lock (mSync)
                {
                    mSubCount--;
                    if (mSubCount == 0)
                    {
                        mInfoToAttachTo.RemoveInput(mInfoToWrap);
                    }
                }
            }

            private sealed class Disposable : IDisposable
            {
                private IDisposable mSubscription;
                private AttachingObservable mParent;

                public Disposable(IDisposable subscription, AttachingObservable parent)
                {
                    mSubscription = subscription;
                    mParent = parent;
                }

                public void Dispose()
                {
                    AttachingObservable parent = Interlocked.Exchange(ref mParent, null);
                    parent?.OnUnsubscribe();
                    mSubscription?.Dispose();
                    mSubscription = null;
                }
            }
        }
    }
}
