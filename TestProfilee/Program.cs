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
        static void Pause()
        {
            Console.WriteLine("Press any key to unsubscribe and finish");
            Console.ReadKey();
        }

        static void Main(string[] args)
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

            Console.WriteLine("Spinning up an observable...");
            IObservable<string> observable = new[] { "One", "two", "three" }.ToObservable()
                .Zip(Observable.Interval(TimeSpan.FromSeconds(1)), (x, _) => x);

            var sub = observable.Subscribe(Console.WriteLine);

            GenericExamples.CallToMethodOnGenericType();

            Pause();

            sub.Dispose();
        }
    }
}
