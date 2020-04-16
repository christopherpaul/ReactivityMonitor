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
        public abstract void AssociateWith(ObservableInfo info);
    }

    internal class DynamicObservableAttacher<T> : DynamicObservableAttacher
    {
        private readonly object mLock = new object();
        private ObservableInfo mInfoToWrap;
        private ObservableInfo mInfoToAttachTo;
        private bool mIsSubscribed;

        public override void AssociateWith(ObservableInfo info)
        {
            lock (mLock)
            {
                mInfoToAttachTo = info;
                if (mIsSubscribed)
                {
                    mInfoToAttachTo.AddInput(mInfoToWrap);
                }
            }
        }

        public IObservable<T> Attach(IObservable<T> observable)
        {
            if (observable is IInstrumentedObservable instrumentedObservable)
            {
                lock (mLock)
                {
                    mInfoToWrap = (ObservableInfo)instrumentedObservable.Info;
                }

                return new AttachingObservable(observable, this);
            }

            return observable;
        }

        private void OnSubscribe()
        {
            lock (mLock)
            {
                mIsSubscribed = true;
                if (mInfoToWrap != null && mInfoToAttachTo != null)
                {
                    mInfoToAttachTo.AddInput(mInfoToWrap);
                }
            }
        }

        private void OnUnsubscribe()
        {
            lock (mLock)
            {
                mIsSubscribed = false;
                if (mInfoToWrap != null && mInfoToAttachTo != null)
                {
                    mInfoToAttachTo.RemoveInput(mInfoToWrap);
                }
            }
        }

        internal sealed class AttachingObservable : IObservable<T>
        {
            private readonly IObservable<T> mObservable;
            private readonly DynamicObservableAttacher<T> mParent;
            private int mSubCount;
            private object mSync = new object();

            public AttachingObservable(IObservable<T> observable, DynamicObservableAttacher<T> parent)
            {
                mObservable = observable;
                mParent = parent;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                lock (mSync)
                {
                    mSubCount++;
                    if (mSubCount == 1)
                    {
                        mParent.OnSubscribe();
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
                        mParent.OnUnsubscribe();
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
