using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
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
            IConnectableObservable<string> observable = new[] { "One", "two", "three" }.ToObservable()
                .Zip(Observable.Interval(TimeSpan.FromSeconds(1)), (x, _) => x)
                .SelectMany(x => x.ToObservable().Select(c => Observable.Return(c).Delay(TimeSpan.FromMilliseconds(100))).Concat())
                .Select(x => $"{x}")
                .Repeat()
                .Publish();

            using (observable.Subscribe(Console.WriteLine))
            using (observable.Connect())
            {
                GenericExamples.CallToMethodOnGenericType();

                Pause();
            }  
        }
    }
}
    