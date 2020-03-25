using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetFrameworkProfilee.Tests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public async Task SimpleTestWithObservable()
        {
            var obs1 = Observable.Interval(TimeSpan.FromSeconds(0.03))
                .Take(10)
                .Select(x => x * x);

            var obs2 = Observable.Interval(TimeSpan.FromSeconds(0.1))
                .Select(x => x * x * x)
                .Select(x => obs1.Select(y => x + y))
                .Take(5)
                .Switch();

            var allValues = await obs2.ToList();

            Assert.That(allValues, Has.Count.EqualTo(22), "count");
        }
    }
}
