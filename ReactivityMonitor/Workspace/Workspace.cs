using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using ReactivityMonitor.Connection;
using ReactivityMonitor.Model;

namespace ReactivityMonitor.Workspace
{
    public sealed class Workspace : IWorkspaceBuilder, IMonitoringConfiguration
    {
        private IConnectionModel mConnectionModel;
        private ISourceCache<IMonitoredCall, int> mMonitoredCalls;
        private ISourceCache<IInstrumentedMethod, int> mMethods;
        private ISourceList<IWorkspaceDocument> mDocuments;

        public Workspace()
        {
            mMonitoredCalls = new SourceCache<IMonitoredCall, int>(c => c.Call.InstrumentedCallId);
            mMethods = new SourceCache<IInstrumentedMethod, int>(m => m.InstrumentedMethodId);
            mDocuments = new SourceList<IWorkspaceDocument>();

            MonitoredCalls = mMonitoredCalls.Connect().RemoveKey();
            Methods = mMethods.Connect().RemoveKey();
            Documents = mDocuments.Connect();

            mDocuments.Add(MonitoringConfiguration);
        }

        public string Name => mConnectionModel.Name;

        public IReactivityModel Model => mConnectionModel.Model;

        public IMonitoringConfiguration MonitoringConfiguration => this;

        public IObservable<IChangeSet<IWorkspaceDocument>> Documents { get; }

        public IEventsDocument CreateEventsDocument(IEnumerable<IInstrumentedCall> calls)
        {
            var callsList = calls.ToArray();
            var doc = new EventsDocument(this);
            doc.AddRange(callsList);
            doc.DocumentName = callsList.Length == 1 ? $"{callsList[0].CalledMethod} - events" : $"{callsList.Length} calls - events";
            mDocuments.Add(doc);
            return doc;
        }

        public IEventsDocument CreateEventsDocument(IEnumerable<IObservableInstance> observableInstances)
        {
            var doc = new EventsDocument(this);
            doc.AddRange(observableInstances);
            doc.DocumentName = "Events";
            mDocuments.Add(doc);
            return doc;
        }

        public IObservable<IChangeSet<IInstrumentedMethod>> Methods { get; }

        public IObservable<IChangeSet<IMonitoredCall>> MonitoredCalls { get; }

        public void AddMethod(IInstrumentedMethod method)
        {
            mMethods.AddOrUpdate(method);
        }

        public void RemoveMethod(IInstrumentedMethod method)
        {
            mMethods.RemoveKey(method.InstrumentedMethodId);
        }

        public void AddSourceMethod(ISourceMethod sourceMethod)
        {
            sourceMethod.Module.InstrumentedMethods
                .Where(m => m.SourceMethod.Equals(sourceMethod))
                .Subscribe(AddMethod);
        }

        public IMonitoredCall StartMonitoringCall(IInstrumentedCall call)
        {
            var monitoredCallOpt = mMonitoredCalls.Lookup(call.InstrumentedCallId);
            if (!monitoredCallOpt.HasValue)
            {
                var monitoredCall = new MonitoredCall(call);
                mMonitoredCalls.AddOrUpdate(monitoredCall);
                return monitoredCall;
            }

            return monitoredCallOpt.Value;
        }

        public void StopMonitoringCall(IInstrumentedCall call)
        {
            mMonitoredCalls.RemoveKey(call.InstrumentedCallId);
        }

        public void Initialise(IConnectionModel connectionModel)
        {
            if (mConnectionModel != null)
            {
                throw new InvalidOperationException($"{nameof(Initialise)} can only be called once.");
            }

            mConnectionModel = connectionModel ?? throw new ArgumentNullException(nameof(connectionModel));

            // Tell model we want to monitor the calls as dictated by the workspace
            MonitoredCalls
                .Transform(call => call.Call.InstrumentedCallId)
                .OnItemAdded(mConnectionModel.StartMonitoringCall)
                .OnItemRemoved(mConnectionModel.StopMonitoringCall)
                .Subscribe();
        }

        public void PauseUpdates()
        {
            mConnectionModel.PauseUpdates();
        }

        public void ResumeUpdates()
        {
            mConnectionModel.ResumeUpdates();
        }

        public void RequestObjectProperties(PayloadObject obj)
        {
            mConnectionModel.RequestObjectProperties(obj.ObjectId);
        }

        IWorkspace IWorkspaceDocument.Workspace => this;
    }
}
