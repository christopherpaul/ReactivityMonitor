using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System.Reactive;
using System.Reactive.Linq;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Utility.Tests
{
    [TestFixture]
    public partial class ObservableExtensionsTests
    {
        [Test]
        public void BeforeFirstGateValue_GateBySequenceNumberHoldsSourceValues()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(true).At(150));

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(150),
                Notification.CreateOnNext(2).At(150),
                Notification.CreateOnNext(3).At(150),
                Notification.CreateOnNext(11).At(150),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void BeforeFirstTrueGateValue_GateBySequenceNumberHoldsSourceValues()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(false).At(50),
                Notification.CreateOnNext(true).At(150));

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(150),
                Notification.CreateOnNext(2).At(150),
                Notification.CreateOnNext(3).At(150),
                Notification.CreateOnNext(11).At(150),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void BetweenFalseAndTrueGateValues_GateBySequenceNumberHoldsSourceValues()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(true).At(50),
                Notification.CreateOnNext(false).At(150),
                Notification.CreateOnNext(true).At(350));

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(50),
                Notification.CreateOnNext(2).At(50),
                Notification.CreateOnNext(3).At(50),
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(350),
                Notification.CreateOnNext(13).At(350),
                Notification.CreateOnCompleted<int>().At(400)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void BetweenFalseAndTrueGateValues_GateBySequenceNumberPassesSourceValuesNoGreaterThanGreatestAlreadySeen()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(5).At(220),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnNext(11).At(320),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(true).At(50),
                Notification.CreateOnNext(false).At(150),
                Notification.CreateOnNext(true).At(350));

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(50),
                Notification.CreateOnNext(2).At(50),
                Notification.CreateOnNext(3).At(50),
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(5).At(220),
                Notification.CreateOnNext(11).At(320),
                Notification.CreateOnNext(12).At(350),
                Notification.CreateOnNext(13).At(350),
                Notification.CreateOnCompleted<int>().At(400)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void WhenSourceCompletesAndGateIsFalse_GateBySequenceNumberHoldsSourceValuesUntilGateBecomesTrue()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(true).At(140),
                Notification.CreateOnNext(false).At(150),
                Notification.CreateOnNext(true).At(500));

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(140),
                Notification.CreateOnNext(2).At(140),
                Notification.CreateOnNext(3).At(140),
                Notification.CreateOnNext(11).At(140),
                Notification.CreateOnNext(12).At(500),
                Notification.CreateOnNext(13).At(500),
                Notification.CreateOnCompleted<int>().At(500)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void WhenGateIsImmediatelyTrue_GateBySequenceNumberPassesImmediateSourceValues()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = Observable.Return(true);

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 10, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(10),
                Notification.CreateOnNext(2).At(10),
                Notification.CreateOnNext(3).At(10),
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void WhenGateTerminates_GateBySequenceNumberPassesAllRemainingSourceValues()
        {
            var testScheduler = new TestScheduler();
            var immediateSourceValues = new[] { 1, 2, 3 }.ToObservable();
            var subsequentSourceValues = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(11).At(100),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400));
            var source = immediateSourceValues.Concat(subsequentSourceValues);

            var gate = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(true).At(40),
                Notification.CreateOnNext(false).At(50),
                Notification.CreateOnCompleted<bool>().At(160));

            var subject = source.GateBySequenceNumber(gate, x => x);

            var observer = testScheduler.Start(() => subject, 0, 10, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(1).At(40),
                Notification.CreateOnNext(2).At(40),
                Notification.CreateOnNext(3).At(40),
                Notification.CreateOnNext(11).At(160),
                Notification.CreateOnNext(12).At(200),
                Notification.CreateOnNext(13).At(300),
                Notification.CreateOnCompleted<int>().At(400)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }
    }
}
