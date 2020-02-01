using System;
using ReactivityMonitor.Model;

namespace ReactivityMonitor.Screens.EventListScreen
{
    public abstract class EventItem
    {
        public static EventItem FromObservableInstance(IObservableInstance obs)
        {
            return new ObservableInstanceEventItem(obs);
        }

        public static EventItem FromStreamEvent(ISubscription sub, StreamEvent e)
        {
            return new StreamEventItem(sub, e);
        }

        public static EventItem FromClientEvent(ClientEvent clientEvent)
        {
            return new ClientEventItem(clientEvent);
        }

        public long SequenceId => Info.SequenceId;

        public abstract EventInfo Info { get; }

        protected abstract IObservableInstance ObservableInstance { get; }

        public long ObservableId => ObservableInstance?.ObservableId ?? 0;
        public abstract long? SubscriptionId { get; }

        public abstract string EventKindName { get; }

        public abstract string Value { get; }

        public abstract PayloadObject Payload { get; }

        public string CalledMethodName => ObservableInstance?.Call.CalledMethod;
        public string CallingMethodName => ObservableInstance?.Call.CallingMethod;

        private sealed class ObservableInstanceEventItem : EventItem
        {
            private readonly IObservableInstance mObs;

            public ObservableInstanceEventItem(IObservableInstance obs)
            {
                mObs = obs;
            }

            public override EventInfo Info => mObs.Created;

            public override long? SubscriptionId => null;

            public override string EventKindName => "Create observable";

            public override string Value => $"{mObs.Call.CallingType}::{mObs.Call.CallingMethod}: {mObs.Call.CalledMethod}";

            public override PayloadObject Payload => null;

            protected override IObservableInstance ObservableInstance => mObs;
        }

        private sealed class StreamEventItem : EventItem
        {
            private readonly ISubscription mSub;
            private readonly StreamEvent mEvent;

            public StreamEventItem(ISubscription sub, StreamEvent @event)
            {
                mSub = sub;
                mEvent = @event;
            }

            public override EventInfo Info => mEvent.Info;

            public override long? SubscriptionId => mSub.SubscriptionId;

            public override string EventKindName => GetEventKindName(mEvent.Kind);

            public override string Value => GetEventValue(mEvent);

            public override PayloadObject Payload => GetPayload(mEvent);

            private PayloadObject GetPayload(StreamEvent e)
            {
                switch (e.Kind)
                {
                    case StreamEvent.EventKind.OnNext:
                        return ((OnNextEvent)e).Payload as PayloadObject;
                    case StreamEvent.EventKind.OnError:
                        return ((OnErrorEvent)e).Exception as PayloadObject;
                    default:
                        return null;
                }
            }

            private static string GetEventValue(StreamEvent e)
            {
                switch (e.Kind)
                {
                    case StreamEvent.EventKind.OnCompleted:
                    case StreamEvent.EventKind.Subscribe:
                    case StreamEvent.EventKind.Unsubscribe:
                        return string.Empty;

                    case StreamEvent.EventKind.OnError: return ((OnErrorEvent)e).Message;
                    case StreamEvent.EventKind.OnNext: return GetOnNextValue(((OnNextEvent)e).Payload);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(e));
                }

                string GetOnNextValue(object payload)
                {
                    if (payload is PayloadObject obj)
                    {
                        return $"{obj.ToString()} [{obj.TypeName}]";
                    }
                    else
                    {
                        return payload?.ToString();
                    }
                }
            }

            protected override IObservableInstance ObservableInstance => mSub.Observable;

            private static string GetEventKindName(StreamEvent.EventKind kind)
            {
                switch (kind)
                {
                    case StreamEvent.EventKind.OnCompleted: return "OnCompleted";
                    case StreamEvent.EventKind.OnError: return "OnError";
                    case StreamEvent.EventKind.OnNext: return "OnNext";
                    case StreamEvent.EventKind.Subscribe: return "Subscribe";
                    case StreamEvent.EventKind.Unsubscribe: return "Unsubscribe";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind));
                }
            }
        }

        private sealed class ClientEventItem : EventItem
        {
            public ClientEventItem(ClientEvent clientEvent)
            {
                ClientEvent = clientEvent;
            }

            public override EventInfo Info => ClientEvent.Info;
            public override long? SubscriptionId => null;
            public override string EventKindName => "Information";
            public override string Value => ClientEvent.Description;
            public override PayloadObject Payload => null;
            public ClientEvent ClientEvent { get; }
            protected override IObservableInstance ObservableInstance => null;
        }
    }
}
