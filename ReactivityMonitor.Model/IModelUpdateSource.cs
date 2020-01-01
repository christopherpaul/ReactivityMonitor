﻿using ReactivityMonitor.Model.ModelUpdate;
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
        IObservable<NewInstrumentedCall> InstrumentedCalls { get; }
        IObservable<NewObservableInstance> ObservableInstances { get; }
        IObservable<NewObservableInstanceLink> ObservableInstanceLinks { get; }
        IObservable<NewSubscription> CreatedSubscriptions { get; }
        IObservable<DisposedSubscription> DisposedSubscriptions { get; }
        IObservable<NewStreamEvent> StreamEvents { get; }
    }
}
