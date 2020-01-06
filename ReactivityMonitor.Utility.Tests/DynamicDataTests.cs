using DynamicData;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Utility.Tests
{
    [TestFixture]
    public class DynamicDataTests
    {
        /// <summary>
        /// This test demonstrates a bug in ObservableCache.
        /// See https://github.com/reactiveui/DynamicData/issues/319
        /// </summary>
        /// <returns></returns>
        [Test, Explicit]
        public async Task ObservableCache_Connect_IsThreadSafeWithRespectToSource()
        {
            object extraLocker = new object();

            var source = Enumerable.Range(1, 10000)
                .ToObservable()
                .SubscribeOn(NewThreadScheduler.Default)
                //.Delay(TimeSpan.FromSeconds(1))
                .Publish();

            var cache = source
                .Synchronize(extraLocker)
                .ToObservableChangeSet(x => x)
                .AsObservableCache();

            source.Connect();
            //await source.LastAsync();
            var changeSets = Observable.Create<IChangeSet<int, int>>(observer =>
            {
                lock (extraLocker)
                {
                    return cache.Connect().Subscribe(observer);
                }
            });
            var allChanges = await changeSets.Flatten().Select(chg => chg.Current).ToList();

            Assert.That(allChanges, Is.EquivalentTo(Enumerable.Range(1, 10000)));
        }
    }
}
