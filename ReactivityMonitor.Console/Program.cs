using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var discovery = new ServerDiscovery();
            discovery.Scan();

            if (discovery.Servers.Count == 0)
            {
                Console.WriteLine("No processes available to connect to.");
                return;
            }

            Console.WriteLine("Available processes:");
            for (int i = 0; i < discovery.Servers.Count; i++)
            {
                var server = discovery.Servers[i];
                Console.WriteLine($"  {i + 1}. {server.ProcessName}");
            }

            Console.WriteLine();
            Console.Write("Connect to> ");
            string input = Console.ReadLine();
            if (!int.TryParse(input, out int option) || option < 1 || option > discovery.Servers.Count)
            {
                Console.WriteLine("Invalid option.");
                return;
            }

            option--;

            var modelSource = new ReactivityModelSource();
            var profilerClient = new ProfilerClient.Client(
                discovery.Servers[option].PipeName,
                modelSource.Updater,
                modelSource.ProfilerControl);
            var model = modelSource.Model;

            using (profilerClient.Connect())
            {
                var moduleEvents = model.Modules.Connect()
                    .SelectMany(changes => changes)
                    .Select(change => change.Current)
                    .Select(module => $"Module: {module.ModuleId:X}: {module.Path}");

                var instrumentedCallEvents = model.InstrumentedCalls.Connect()
                    .SelectMany(changes => changes)
                    .Select(change => change.Current)
                    .Select(ic => $"Call: {ic.InstrumentedCallId} in module {ic.Module.ModuleId:X}");

                Console.WriteLine("Printing events...any key to end.");
                using (Observable.Merge(moduleEvents, instrumentedCallEvents)
                    .Subscribe(Console.WriteLine))
                {
                    Console.ReadKey();
                }
            }
        }
    }
}
