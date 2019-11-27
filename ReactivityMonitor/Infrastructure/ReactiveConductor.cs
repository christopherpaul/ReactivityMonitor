using Caliburn.Micro;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Infrastructure
{
    public abstract class ReactiveConductor<T> : Conductor<T>, IActivatableViewModel
        where T: class
    {
        private readonly ViewModelActivator mActivator = new ViewModelActivator();
        ViewModelActivator IActivatableViewModel.Activator => mActivator;

        protected sealed override void OnActivate()
        {
            base.OnActivate();
            mActivator.Activate();
        }

        protected sealed override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            mActivator.Deactivate(ignoreRefCount: true);
        }

        protected void WhenActivated(Action<CompositeDisposable> activationBlock) =>
            ((IActivatableViewModel)this).WhenActivated(activationBlock);
    }
}
