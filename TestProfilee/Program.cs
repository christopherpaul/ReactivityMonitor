using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I'm really here");
            Console.WriteLine(Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING"));
            Console.WriteLine(Environment.GetEnvironmentVariable("COR_PROFILER"));

            //IObservable<string> observable = new[] { "One", "two", "three" }.ToObservable()
            //    .Zip(Observable.Interval(TimeSpan.FromSeconds(1)), (x, _) => x);

            var observable = Observable.Interval(TimeSpan.FromSeconds(1));
            observable.Subscribe(Console.WriteLine);

            Console.ReadKey();
        }
    }
}
