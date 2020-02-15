using ReactivityMonitor.Model.ModelUpdate;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReactivityMonitor.Model
{
    public interface IModelUpdateSource
    {
        IDisposable Connect();
        void Pause();
        void Resume();

        IObservable<NewModuleUpdate> Modules { get; }
        IObservable<NewInstrumentedMethod> InstrumentedMethods { get; }
        IObservable<NewObservableInstance> ObservableInstances { get; }
        IObservable<NewObservableInstanceLink> ObservableInstanceLinks { get; }
        IObservable<NewSubscription> CreatedSubscriptions { get; }
        IObservable<DisposedSubscription> DisposedSubscriptions { get; }
        IObservable<NewStreamEvent> StreamEvents { get; }
        IObservable<NewTypeInfo> Types { get; }
        IObservable<ObjectPropertiesInfo> ObjectPropertiesInfos { get; }
        IObservable<ClientEvent> ClientEvents { get; }
    }
}
