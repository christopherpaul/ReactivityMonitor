using Caliburn.Micro;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Infrastructure
{
    public abstract class ReactiveScreen : Screen, IActivatableViewModel
    {
        private readonly ViewModelActivator mActivator = new ViewModelActivator();
        public ViewModelActivator Activator => mActivator;

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

        protected void Set<T>(BehaviorSubject<T> subject, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(value, subject.Value))
            {
                subject.OnNext(value);
                NotifyOfPropertyChange(propertyName);
            }
        }
    }
}
