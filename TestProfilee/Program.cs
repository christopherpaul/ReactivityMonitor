using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    class Program
    {
        //static Program()
        //{
        //    Console.WriteLine("STATIC INIT");
        //    DummySupportAssemblyResolution.EnsureHandler();
        //}

        static void Pause()
        {
            Console.WriteLine("Press any key to unsubscribe and finish");
            Console.ReadKey();
        }

        static void Main(string[] args)
        {
            Main2(args);
        }

        static void Main2(string[] args)
        {
            string[] envVars =
            {
                "COR_ENABLE_PROFILING",
                "COR_PROFILER" 
            };  
            Console.WriteLine($"Profiling environment vars:");
            foreach (string name in envVars)
            {
                Console.WriteLine($"\t{name} = {Environment.GetEnvironmentVariable(name)}");
            }
            Console.WriteLine();

            int typingSpeed = 100;

            Console.WriteLine("Spinning up an observable...");
            IConnectableObservable<string> observable = new[] { "One", "two", "three" }.ToObservable()
                .Zip(Observable.Interval(TimeSpan.FromSeconds(1)), (x, _) => x)
                .SelectMany(x => x.ToObservable().Select(c => Observable.Return(c).Delay(TimeSpan.FromMilliseconds(typingSpeed))).Concat())
                .Select((x, i) => new ExampleObject($"{x}", i) { Nested = new NestedObject($"{Environment.TickCount} ticks") })
                .Repeat()
                .Select(x => x.Str)
                .Publish();

            ConstrainedGenericExample.Test(new ObjectWithObservableProperty());

            using (observable.Subscribe(Console.WriteLine))
            using (observable.Connect())
            using (TestGroupedObservable().Subscribe(Console.WriteLine))
            using (Observable.FromAsync(AsyncMethod).Subscribe(Console.WriteLine))
            using (Observable.FromAsync(async () => await Observable.Interval(TimeSpan.FromSeconds(3)).Take(3)).Subscribe(Console.WriteLine))
            {
                GenericExamples.CallToMethodOnGenericType();

                Pause();
            }  
        }

        static IObservable<string> TestGroupedObservable()
        {
            return Observable.Interval(TimeSpan.FromSeconds(1))
                .GroupBy(n => n % 2 == 0)
                .SelectMany(g => DummyFunctionForTest(g).Select(x => $"{x}: {g.Key}"));
        }

        static IGroupedObservable<bool, long> DummyFunctionForTest(IGroupedObservable<bool, long> g)
        {
            Console.WriteLine($"Group: {g.Key}");
            return g;
        }

        static async Task<string> AsyncMethod()
        {
            string result = await Observable.Return("async")
                .Delay(TimeSpan.FromSeconds(5));

            return $"{result} {result} {result}";
        }
    }
}
    