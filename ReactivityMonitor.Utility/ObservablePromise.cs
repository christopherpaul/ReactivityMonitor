using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Utility
{
    public sealed class ObservablePromise<T> : IObservable<T>
    {
        private readonly TaskCompletionSource<IObservable<T>> mTcs;

        public ObservablePromise()
        {
            mTcs = new TaskCompletionSource<IObservable<T>>();
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            return mTcs.Task.ToObservable().Switch().Subscribe(observer);
        }

        public void Resolve(IObservable<T> observable)
        {
            mTcs.SetResult(observable);
        }
    }
}
