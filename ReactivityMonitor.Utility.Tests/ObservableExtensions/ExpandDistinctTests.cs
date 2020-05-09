using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using ReactivityMonitor.Utility.Extensions;
using static System.Reactive.Notification;

namespace ReactivityMonitor.Utility.Tests
{
    [TestFixture]
    public class ExpandDistinctTests
    {
        [Test]
        public void GivenEmptySource_ThenExpandDistinctCompletesImmediately()
        {
            var testScheduler = new PredictableTestScheduler();

            var observer = testScheduler.Start(
                () => Observable.Empty<int>().ExpandDistinct(_ => Observable.Empty<int>(), testScheduler),
                10,
                20,
                30);

            var expected = new[]
            {
                CreateOnCompleted<int>().At(20)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void GivenSimpleSourceAndEmptySelector_ThenExpandDistinctMatchesSource()
        {
            var testScheduler = new PredictableTestScheduler();

            var observer = testScheduler.Start(
                () => new[] { 3, 2, 1 }.ToObservable().ExpandDistinct(_ => Observable.Empty<int>(), testScheduler),
                10,
                20,
                30);

            var expected = new[]
            {
                CreateOnNext(3).At(20),
                CreateOnNext(2).At(20),
                CreateOnNext(1).At(20),
                CreateOnCompleted<int>().At(20)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void GivenImmediateSourceAndSelector_ExpandDistinctProducesAllReturnsImmediatelyAndCompletes()
        {
            var testScheduler = new PredictableTestScheduler();

            var observer = testScheduler.Start(
                () => new[] { 10, 15 }.ToObservable().ExpandDistinct(x => x > 0 ? Observable.Return(x / 2) : Observable.Empty<int>(), testScheduler),
                10,
                20,
                30);

            var expected = new[]
            {
                CreateOnNext(10).At(20),
                CreateOnNext(5).At(20),
                CreateOnNext(2).At(20),
                CreateOnNext(1).At(20),
                CreateOnNext(0).At(20),
                CreateOnNext(15).At(20),
                CreateOnNext(7).At(20),
                CreateOnNext(3).At(20),
                CreateOnCompleted<int>().At(20)
            };

            Assert.That(observer.Messages, Is.EquivalentTo(expected));
        }

        [Test]
        public void GivenHotSource_AndHotExpansions_ExpandDistinctBehaves()
        {
            var testScheduler = new PredictableTestScheduler();

            var observer = testScheduler.Start(
                () => testScheduler.CreateHotObservable(CreateOnNext(10).At(10), CreateOnNext(16).At(20), CreateOnCompleted<int>().At(22))
                        .ExpandDistinct(x => x > 0 
                            ? testScheduler.CreateHotObservable(CreateOnNext(x/2).At(testScheduler.Clock + 2), CreateOnNext(x/3).At(testScheduler.Clock + 3), CreateOnCompleted<int>().At(testScheduler.Clock + 4)) 
                            : Observable.Empty<int>(), testScheduler),
                0,
                5,
                100);

            var expected = new[]
            {
                CreateOnNext(10).At(10),
                CreateOnNext(16).At(20),
                CreateOnNext(5).At(12),
                CreateOnNext(3).At(13),
                CreateOnNext(8).At(22),
                CreateOnNext(2).At(14),
                CreateOnNext(1).At(15),
                CreateOnNext(0).At(17),
                CreateOnNext(4).At(24),
                CreateOnCompleted<int>().At(28)
            };

            Assert.That(observer.Messages, Is.EquivalentTo(expected));
        }

    }
}
