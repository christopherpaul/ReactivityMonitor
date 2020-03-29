using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Protocol = ReactivityProfiler.Protocol;
using Google.Protobuf;
using ReactivityMonitor.Utility.Extensions;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using ReactivityMonitor.Model;

namespace ReactivityMonitor.ProfilerClient
{
    public sealed class Connection
    {
        private static readonly Protocol.RequestMessage cStartSendingInstrumentationEvents = new Protocol.RequestMessage
        {
            SendInstrumentationEvents = new Protocol.SendInstrumentationEventsRequest()
        };

        private readonly ISubject<IObservable<byte[]>> mIncomingRawMessageStreams;
        private readonly string mPipeName;
        private readonly IProfilerControl mProfilerControl;

        public static Connection Create(string pipeName, IProfilerControl profilerControl)
        {
            return new Connection(pipeName, profilerControl);
        }

        private Connection(string pipeName, IProfilerControl profilerControl)
        {
            mPipeName = pipeName;
            mProfilerControl = profilerControl;

            mIncomingRawMessageStreams = new BehaviorSubject<IObservable<byte[]>>(Observable.Empty<byte[]>());
            var whenConnected = new Subject<bool>();

            var incomingMessages = mIncomingRawMessageStreams
                .Select(stream => Observable.Defer(() =>
                {
                    whenConnected.OnNext(true);
                    return stream.Finally(() => whenConnected.OnNext(false));
                }))
                .Switch()
                .Select(Protocol.EventMessage.Parser.ParseFrom);

            ModelUpdateSource = new ModelUpdateSource(incomingMessages);
            WhenConnected = whenConnected.Publish(false).ConnectForEver();
        }

        public IModelUpdateSource ModelUpdateSource { get; }

        public IObservable<bool> WhenConnected { get; }

        public IDisposable Connect()
        {
            var disposables = new CompositeDisposable();

            var outgoingMessages = mProfilerControl.GetControlMessages()
                .StartWith(cStartSendingInstrumentationEvents)
                .TakeUntilDisposed(disposables)
                .Select(msg => msg.ToByteArray())
                .MonitorSubscriptionCount(out var whenOutgoingMessagesSubscriptionCountChanges);

            ProfilerCommunication.CreateRawChannel(mPipeName, outgoingMessages)
                .Take(1) // for now just deal with the first connection
                .Subscribe(stream => mIncomingRawMessageStreams.OnNext(stream));

            disposables.Add(ModelUpdateSource.Connect());

            return disposables;
        }
    }
}
