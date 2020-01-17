using NUnit.Framework;
using ReactivityMonitor.Model.ModelUpdate;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Protocol = ReactivityProfiler.Protocol;

namespace ReactivityMonitor.ProfilerClient.Tests
{
    [TestFixture]
    public class ModelUpdateSourceTests
    {
        private static IEnumerable<TestCaseData> GetValueExamples()
        {
            yield return new TestCaseData(
                @"{""DateTimeUtc"": ""637148838788460706""}",
                new DateTime(637148838788460706, DateTimeKind.Utc))
                .SetName("{m}(OnNext(DateTimeUtc))");
        }

        public static IEnumerable<TestCaseData> ValueExamples => GetValueExamples();

        [TestCaseSource(nameof(ValueExamples))]
        public void HandlesAllValueKinds(string json, object expectedValue)
        {
            var valueField = Protocol.Value.Parser.ParseJson(json);
            var message = new Protocol.EventMessage
            {
                OnNext = new Protocol.OnNextEvent
                {
                    Event = new Protocol.EventInfo
                    {
                        SequenceId = 1,
                        ThreadId = 2,
                        Timestamp = 3
                    },
                    SubscriptionId = 4,
                    Value = valueField
                }
            };

            var messageSource = Observable.Return(message);

            var modelUpdateSource = new ModelUpdateSource(messageSource);

            var outputs = modelUpdateSource.StreamEvents
                .Select(x => x.Payload)
                .OfType<SimplePayloadInfo>()
                .Select(x => x.Value);

            object received = null;
            outputs.Subscribe(x => received = x);

            modelUpdateSource.Connect();

            Assert.That(received, Is.EqualTo(expectedValue));
        }
    }
}
