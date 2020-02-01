using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reactive;
using System.Reactive.Linq;
using ReactivityMonitor.Utility.Extensions;
using DynamicData;

namespace ReactivityMonitor.Utility.Tests
{
    [TestFixture]
    public class ObservableCacheExtensionsTests
    {
        [Test]
        public void SemiJoinOnRightKey_LeftItemsBeforeMatchingRightItem_ArePropagated()
        {
            var testScheduler = new TestScheduler();
            var left = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(100).At(100),
                Notification.CreateOnNext(101).At(101))
                .ToObservableChangeSet(x => x);

            var right = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(1).At(200))
                .ToObservableChangeSet(x => x);

            var subject = left.SemiJoinOnRightKey(right, x => x / 100)
                .Flatten()
                .Select(c => c.Current);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(100).At(200),
                Notification.CreateOnNext(101).At(200)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }

        [Test]
        public void SemiJoinOnRightKey_LeftItemsAfterMatchingRightItem_ArePropagated()
        {
            var testScheduler = new TestScheduler();
            var left = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(100).At(100),
                Notification.CreateOnNext(101).At(101))
                .ToObservableChangeSet(x => x);

            var right = testScheduler.CreateHotObservable(
                Notification.CreateOnNext(1).At(50))
                .ToObservableChangeSet(x => x);

            var subject = left.SemiJoinOnRightKey(right, x => x / 100)
                .Flatten()
                .Select(c => c.Current);

            var observer = testScheduler.Start(() => subject, 0, 0, 1000);

            var expected = new[]
            {
                Notification.CreateOnNext(100).At(100),
                Notification.CreateOnNext(101).At(101)
            };

            Assert.That(observer.Messages, Is.EqualTo(expected));
        }
    }
}
