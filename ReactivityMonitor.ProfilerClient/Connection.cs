using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Protocol = ReactivityProfiler.Protocol;
using Google.Protobuf;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.ProfilerClient
{
    public sealed class Connection
    {
        private static readonly Protocol.RequestMessage cStartSendingInstrumentationEvents = new Protocol.RequestMessage
        {
            SendInstrumentationEvents = new Protocol.SendInstrumentationEventsRequest()
        };
        private readonly Func<IDisposable> mConnect;

        public static Connection Create(string pipeName, Model.IProfilerControl profilerControl)
        {
            var outgoingMessages = profilerControl.GetControlMessages()
                .StartWith(cStartSendingInstrumentationEvents)
                .Select(msg => msg.ToByteArray())
                .MonitorSubscriptionCount(out var whenOutgoingMessagesSubscriptionCountChanges);

            var incomingMessages = ProfilerCommunication.CreateRawChannel(pipeName, outgoingMessages)
                .Take(1) // for now just deal with the first connection
                .Merge()
                .Select(Protocol.EventMessage.Parser.ParseFrom);

            var modelUpdateSource = new ModelUpdateSource(incomingMessages);

            var whenConnected = whenOutgoingMessagesSubscriptionCountChanges
                .Select(c => c > 0)
                .DistinctUntilChanged()
                .Publish(false)
                .ConnectForEver();

            return new Connection(modelUpdateSource, modelUpdateSource.Connect, whenConnected);
        }

        private Connection(Model.IModelUpdateSource modelUpdateSource, Func<IDisposable> connect, IObservable<bool> whenConnected)
        {
            ModelUpdateSource = modelUpdateSource;
            mConnect = connect;
            WhenConnected = whenConnected;
        }

        public Model.IModelUpdateSource ModelUpdateSource { get; }

        public IObservable<bool> WhenConnected { get; }

        public IDisposable Connect() => mConnect();
    }
}
